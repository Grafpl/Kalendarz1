using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Kalendarz1.WPF
{
    // ════════════════════════════════════════════════════════════
    // Convertery dla nagłówków grup statusów
    // ════════════════════════════════════════════════════════════
    public class StatusToIconConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value as string) switch
            {
                "⚠ Do zatwierdzenia"                  => "⚠",
                "⚠ Zafakturowane — zmiana po fakturze" => "⚠",
                "Nowe"                                 => "✦",
                "W realizacji"                         => "◐",
                "Zrealizowane"                         => "✓",
                "Wydano"                               => "▶",
                "Zafakturowane"                        => "✓✓",
                _                                      => "•"
            };
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class StatusToColorConverter : System.Windows.Data.IValueConverter
    {
        private static readonly Brush _warning = Freeze(new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06))); // amber
        private static readonly Brush _danger  = Freeze(new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))); // red - dla zmian po fakturze (poważne!)
        private static readonly Brush _newClr  = Freeze(new SolidColorBrush(Color.FromRgb(0x85, 0x4D, 0x0E))); // yellow-700
        private static readonly Brush _running = Freeze(new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB))); // blue
        private static readonly Brush _done    = Freeze(new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69))); // emerald
        private static readonly Brush _shipped = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x40, 0xAF))); // dark blue
        private static readonly Brush _invoiced= Freeze(new SolidColorBrush(Color.FromRgb(0x06, 0x5F, 0x46))); // emerald-800
        private static readonly Brush _other   = Freeze(new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))); // gray
        private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value as string) switch
            {
                "⚠ Do zatwierdzenia"                  => _warning,
                "⚠ Zafakturowane — zmiana po fakturze" => _danger,
                "Nowe"                                 => _newClr,
                "W realizacji"                         => _running,
                "Zrealizowane"                         => _done,
                "Wydano"                               => _shipped,
                "Zafakturowane"                        => _invoiced,
                _                                      => _other
            };
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public partial class PanelFakturWindow : Window, INotifyPropertyChanged
    {
        // ────────────────────────────────────────────────────────────
        // Połączenia
        // ────────────────────────────────────────────────────────────
        private readonly string _connLibra     = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;Connection Timeout=8";
        private readonly string _connHandel    = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True;Connection Timeout=8";
        private readonly string _connTransport = "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True;Connection Timeout=8";

        public string UserID { get; set; } = string.Empty;

        // ────────────────────────────────────────────────────────────
        // Stan UI
        // ────────────────────────────────────────────────────────────
        private DateTime _selectedDate;
        private int? _currentOrderId;
        private bool _showFakturowane = true;   // ZAWSZE true - zafakturowane zawsze widoczne (na dole grupowania)
        private int? _selectedProductId = null;
        private bool _isRefreshing = false;
        private bool _pendingRefresh = false;

        private readonly DataTable _dtDetails = new();
        private readonly DataTable _dtInvoice = new();
        private readonly ObservableCollection<InvoicePositionCompare> _invoiceCompareItems = new();
        private readonly ObservableCollection<ProductFlowItem> _flowItems = new();

        // Cache (static — przeżywa zamknięcia okna)
        private static readonly Dictionary<int, (string Name, string Salesman)> _contractorsCache = new();
        private static readonly Dictionary<int, string> _productCodeCache = new();   // Id → kod
        private static readonly Dictionary<int, string> _productNameCache = new();   // Id → nazwa (pełna)
        private static readonly Dictionary<int, BitmapImage> _productImagesCache = new(); // Id → obrazek (z dbo.TowarZdjecia)
        private static bool _productImagesLoaded = false;

        // Avatary handlowców (jak w MainWindow + HandlowiecDashboardWindow)
        private static readonly Dictionary<string, BitmapSource> _handlowiecAvatarCache = new(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string>? _handlowiecMapowanie = null;
        private static bool _columnsEnsured = false;
        private static DateTime _contractorsCacheLoadedAt = DateTime.MinValue;

        // CancellationToken dla SelectionChanged (likwiduje race condition)
        private CancellationTokenSource? _detailsCts;

        // Filtrowanie listy zamówień po statusie weryfikacji faktury
        private System.Windows.Data.ListCollectionView? _ordersView;
        private FilterMode _filterMode = FilterMode.None;

        private enum FilterMode
        {
            None,         // pokaż wszystko
            AlarmOnly,    // tylko NotFound + ClientMismatch
            WarnOnly,     // tylko QtyMismatch
            OkOnly,       // tylko Match
            MismatchAll   // alarm + warn (z buttona "Pokaż tylko niezgodne")
        }

        // Undo
        private DispatcherTimer? _undoTimer;
        private Func<Task>? _undoAction;

        // Layout state (do persistence)
        private double _savedRightWidth = 520;
        private string _density = "Compact";   // Domyślnie kompaktowa — clean, dużo wierszy widocznych

        public ObservableCollection<ZamowienieViewModel> ZamowieniaList { get; private set; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public PanelFakturWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            DataContext = this;
            Loaded += PanelFakturWindow_Loaded;
            Closed += PanelFakturWindow_Closed;
            KeyDown += PanelFakturWindow_KeyDown;
            // Grid zakładki "Zam / Wyd / Fak" — kolumny i ItemsSource ustawiamy raz,
            // żeby ObservableCollection.Add() w LoadProductFlowAsync od razu renderował wiersze.
            SetupFlowGrid();
        }

        // ════════════════════════════════════════════════════════════
        // ŻYCIE OKNA
        // ════════════════════════════════════════════════════════════
        private async void PanelFakturWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
            ApplyDensity(_density, persist: false);

            _selectedDate = DateTime.Today;
            UpdateDateDisplay();

            // Lookups równolegle (wcześniej szły szeregowo)
            await Task.WhenAll(
                LoadContractorsCacheAsync(),
                LoadProductsCacheAsync(),
                LoadProductImagesAsync(),
                EnsureHandlowiecMappingLoadedAsync()
            );
            BuildProductCombo();

            // EnsureColumnsExist tylko raz na uruchomienie aplikacji (nie per refresh!)
            if (!_columnsEnsured)
            {
                try
                {
                    await using var cn = new SqlConnection(_connLibra);
                    await cn.OpenAsync();
                    await EnsureColumnsExistAsync(cn);
                    _columnsEnsured = true;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"EnsureColumns error: {ex.Message}"); }
            }

            ApplyResponsiveLayout(ActualWidth);
            await RefreshDataAsync();
        }

        private void PanelFakturWindow_Closed(object? sender, EventArgs e)
        {
            _detailsCts?.Cancel();
            _undoTimer?.Stop();
            SaveSettings();
        }

        // ════════════════════════════════════════════════════════════
        // RESPONSYWNY LAYOUT (Wide / Medium / Narrow)
        // ════════════════════════════════════════════════════════════
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
            => ApplyResponsiveLayout(e.NewSize.Width);

        private void ApplyResponsiveLayout(double width)
        {
            if (rootGrid == null || colSplitter == null || colRight == null || splitter == null || paneRight == null)
                return;

            if (width >= 1200)
            {
                // WIDE: pełne split
                colSplitter.Width = new GridLength(6);
                colRight.Width    = new GridLength(Math.Max(420, _savedRightWidth));
                splitter.Visibility = Visibility.Visible;
                paneRight.Visibility = Visibility.Visible;
            }
            else if (width >= 700)
            {
                // MEDIUM: prawy panel węższy, splitter ukryty (auto width)
                colSplitter.Width = new GridLength(0);
                colRight.Width    = new GridLength(400);
                splitter.Visibility = Visibility.Collapsed;
                paneRight.Visibility = Visibility.Visible;
            }
            else
            {
                // NARROW: tylko lewa, prawy ukryty
                colSplitter.Width = new GridLength(0);
                colRight.Width    = new GridLength(0);
                splitter.Visibility = Visibility.Collapsed;
                paneRight.Visibility = Visibility.Collapsed;
            }
        }

        // ════════════════════════════════════════════════════════════
        // DENSITY (Compact / Default / Comfort) — 3-segment toggle
        // ════════════════════════════════════════════════════════════
        private bool _densitySyncing = false;

        private void DensityRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (_densitySyncing) return;
            if (sender is RadioButton rb && rb.Tag is string tag)
                ApplyDensity(tag, persist: true);
        }

        private void ApplyDensity(string density, bool persist)
        {
            _density = density;
            switch (density)
            {
                case "Compact":
                    Resources["Density.Row.Height"]        = 30.0;
                    Resources["Density.Row.HeightDetails"] = 24.0;
                    Resources["Density.Row.Font"]          = 11.0;
                    Resources["Density.Cell.Pad"]          = new Thickness(8, 3, 8, 3);
                    break;
                case "Comfort":
                    Resources["Density.Row.Height"]        = 52.0;
                    Resources["Density.Row.HeightDetails"] = 40.0;
                    Resources["Density.Row.Font"]          = 14.0;
                    Resources["Density.Cell.Pad"]          = new Thickness(14, 10, 14, 10);
                    break;
                default: // Default
                    Resources["Density.Row.Height"]        = 40.0;
                    Resources["Density.Row.HeightDetails"] = 32.0;
                    Resources["Density.Row.Font"]          = 12.0;
                    Resources["Density.Cell.Pad"]          = new Thickness(10, 6, 10, 6);
                    break;
            }

            // Sync radiobuttonów jeśli zmiana z code-behind (przy starcie z settings)
            _densitySyncing = true;
            try
            {
                if (rbDensityCompact != null) rbDensityCompact.IsChecked = density == "Compact";
                if (rbDensityDefault != null) rbDensityDefault.IsChecked = density == "Default";
                if (rbDensityComfort != null) rbDensityComfort.IsChecked = density == "Comfort";
            }
            finally { _densitySyncing = false; }

            if (persist) SaveSettings();
        }

        // ════════════════════════════════════════════════════════════
        // PERSISTENCE (settings.json)
        // ════════════════════════════════════════════════════════════
        private static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Kalendarz1", "PanelFaktur.settings.json");

        private class PanelSettings
        {
            public string? Density { get; set; }
            public double? RightWidth { get; set; }
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return;
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<PanelSettings>(json);
                if (s != null)
                {
                    if (!string.IsNullOrEmpty(s.Density)) _density = s.Density;
                    if (s.RightWidth.HasValue && s.RightWidth.Value >= 420 && s.RightWidth.Value <= 780)
                        _savedRightWidth = s.RightWidth.Value;
                }
            }
            catch { /* settings opcjonalne */ }
        }

        private void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                // Aktualna szerokość prawego panelu (gdy w trybie Wide)
                double rightWidth = _savedRightWidth;
                if (colRight != null && colRight.ActualWidth >= 420 && splitter?.Visibility == Visibility.Visible)
                    rightWidth = colRight.ActualWidth;

                var s = new PanelSettings
                {
                    Density = _density,
                    RightWidth = rightWidth
                };
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        // ════════════════════════════════════════════════════════════
        // NAWIGACJA DAT — DatePicker (Popup z Calendar) + ‹ › prev/next + Dziś
        // ════════════════════════════════════════════════════════════
        private static readonly string[] _dniTygodnia =
            { "Niedziela", "Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek", "Sobota" };
        private static readonly string[] _miesiace =
            { "", "stycznia", "lutego", "marca", "kwietnia", "maja", "czerwca",
              "lipca", "sierpnia", "września", "października", "listopada", "grudnia" };
        private static readonly string[] _miesiaceSkrot =
            { "", "sty", "lut", "mar", "kwi", "maj", "cze", "lip", "sie", "wrz", "paź", "lis", "gru" };

        private void UpdateDateDisplay()
        {
            // Hero: "Czwartek, 18 marca 2026"
            var dn = _dniTygodnia[(int)_selectedDate.DayOfWeek];
            txtSelectedDate.Text = $"{dn}, {_selectedDate.Day} {_miesiace[_selectedDate.Month]} {_selectedDate.Year}";

            // Helper: "Tydzień 12 · 18-24 mar"
            int week = ISOWeek.GetWeekOfYear(_selectedDate);
            int delta = ((int)_selectedDate.DayOfWeek + 6) % 7; // pon = 0
            DateTime startOfWeek = _selectedDate.AddDays(-delta);
            DateTime endOfWeek   = startOfWeek.AddDays(6);
            string range = startOfWeek.Month == endOfWeek.Month
                ? $"{startOfWeek.Day}–{endOfWeek.Day} {_miesiaceSkrot[endOfWeek.Month]}"
                : $"{startOfWeek.Day} {_miesiaceSkrot[startOfWeek.Month]} – {endOfWeek.Day} {_miesiaceSkrot[endOfWeek.Month]}";
            txtWeekInfo.Text = $"Tydzień {week}  ·  {range}";
        }

        private void BtnDate_Click(object sender, RoutedEventArgs e)
        {
            calendarPicker.SelectedDate = _selectedDate;
            calendarPicker.DisplayDate = _selectedDate;
            popupCalendar.IsOpen = true;
        }

        private async void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            if (calendarPicker.SelectedDate.HasValue)
            {
                var d = calendarPicker.SelectedDate.Value.Date;
                if (d != _selectedDate)
                {
                    _selectedDate = d;
                    UpdateDateDisplay();
                    popupCalendar.IsOpen = false;
                    await RefreshDataAsync();
                }
                else
                {
                    popupCalendar.IsOpen = false;
                }
            }
        }

        private async void BtnPrevDay_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-1);
            UpdateDateDisplay();
            await RefreshDataAsync();
        }

        private async void BtnNextDay_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(1);
            UpdateDateDisplay();
            await RefreshDataAsync();
        }

        private async void BtnToday_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = DateTime.Today;
            UpdateDateDisplay();
            await RefreshDataAsync();
        }

        // Otwiera okno nowego zamowienia (jak "+Nowe" w MainWindow → Zamowienia Klientow)
        private async void BtnNoweZamowienie_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var widok = new Kalendarz1.WidokZamowienia(UserID, null);
                widok.ShowDialog();
                // Po zamknieciu okna - odswiez liste, bo moglo powstac nowe zamowienie
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Nie udało się otworzyć okna nowego zamówienia:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await RefreshDataAsync();

        // ════════════════════════════════════════════════════════════
        // CACHE LOOKUPS (kontrahenci, towary)
        // ════════════════════════════════════════════════════════════
        private async Task LoadContractorsCacheAsync()
        {
            // Static cache — odśwież raz na 30 min
            if (_contractorsCache.Count > 0 && (DateTime.Now - _contractorsCacheLoadedAt).TotalMinutes < 30)
                return;

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                const string sql = @"SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val
                                     FROM [HANDEL].[SSCommon].[STContractors] c
                                     LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId";
                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                _contractorsCache.Clear();
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    string shortcut = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    string salesman = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    _contractorsCache[id] = (shortcut, salesman);
                }
                _contractorsCacheLoadedAt = DateTime.Now;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadContractors error: {ex.Message}"); }
        }

        private async Task LoadProductsCacheAsync()
        {
            if (_productCodeCache.Count > 0 && _productNameCache.Count > 0) return;

            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                // Pełne dane towarów w jednym zapytaniu — likwiduje N+1 z LoadOrderDetails
                const string sql = @"SELECT ID, kod, ISNULL(nazwa, '') FROM [HANDEL].[HM].[TW]
                                     WHERE katalog IN (67095, 67153)
                                     ORDER BY kod";
                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                _productCodeCache.Clear();
                _productNameCache.Clear();
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    string kod = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    string nazwa = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    if (!string.IsNullOrWhiteSpace(kod)) _productCodeCache[id] = kod;
                    _productNameCache[id] = !string.IsNullOrWhiteSpace(nazwa) ? nazwa : kod;
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadProducts error: {ex.Message}"); }
        }

        // Ładuje obrazki towarów z LibraNet.dbo.TowarZdjecia (raz na uruchomienie)
        // Wzorzec skopiowany z MagazynPanel.xaml.cs
        private async Task LoadProductImagesAsync()
        {
            if (_productImagesLoaded) return;
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Sprawdź czy tabela istnieje
                await using var cmdCheck = new SqlCommand(
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'TowarZdjecia') THEN 1 ELSE 0 END", cn);
                if ((int)(await cmdCheck.ExecuteScalarAsync())! == 0)
                {
                    _productImagesLoaded = true; // brak tabeli = nigdy nie próbuj ponownie
                    return;
                }

                await using var cmd = new SqlCommand("SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1", cn);
                cmd.CommandTimeout = 30;
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    int towarId = rdr.GetInt32(0);
                    if (rdr.IsDBNull(1)) continue;
                    try
                    {
                        byte[] data = (byte[])rdr[1];
                        using var ms = new System.IO.MemoryStream(data);
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = ms;
                        bi.DecodePixelWidth = 80;   // miniatura — wystarczy do wierszy
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        _productImagesCache[towarId] = bi;
                    }
                    catch (Exception exImg)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PanelFaktur] Błąd dekodowania obrazka TowarId={towarId}: {exImg.Message}");
                    }
                }
                _productImagesLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PanelFaktur] Błąd ładowania obrazków: {ex.Message}");
            }
        }

        // Ładuje mapping HandlowiecName → UserID z LibraNet.UserHandlowcy
        private async Task EnsureHandlowiecMappingLoadedAsync()
        {
            if (_handlowiecMapowanie != null) return;
            _handlowiecMapowanie = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                    _handlowiecMapowanie[rd.GetString(0)] = rd.GetString(1);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadHandlowiecMapping error: {ex.Message}"); }
        }

        // Cache'uje avatar handlowca (taki sam wzorzec jak w MainWindow + HandlowiecDashboard)
        private void EnsureHandlowiecAvatarCached(string handlowiec, int size = 48)
        {
            if (string.IsNullOrEmpty(handlowiec)) return;
            if (_handlowiecAvatarCache.ContainsKey(handlowiec)) return;
            if (_handlowiecMapowanie == null) return;

            BitmapSource? avatarBmp = null;
            if (_handlowiecMapowanie.TryGetValue(handlowiec, out var uid))
            {
                try
                {
                    if (UserAvatarManager.HasAvatar(uid))
                        using (var av = UserAvatarManager.GetAvatarRounded(uid, size))
                            if (av != null) avatarBmp = ConvertImageToBitmapSource(av);
                    if (avatarBmp == null)
                        using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, uid, size))
                            avatarBmp = ConvertImageToBitmapSource(defAv);
                }
                catch { }
            }
            if (avatarBmp == null)
            {
                try
                {
                    using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, handlowiec, size))
                        avatarBmp = ConvertImageToBitmapSource(defAv);
                }
                catch { }
            }
            if (avatarBmp != null)
            {
                avatarBmp.Freeze();
                _handlowiecAvatarCache[handlowiec] = avatarBmp;
            }
        }

        private static BitmapSource? ConvertImageToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;
            using var bitmap = new System.Drawing.Bitmap(image);
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(hBitmap); }
        }

        private void BuildProductCombo()
        {
            cmbProduct.Items.Clear();
            cmbProduct.Items.Add(new ComboBoxItem { Content = "Wszystkie", Tag = (int?)null });
            foreach (var p in _productCodeCache.OrderBy(x => x.Value))
                cmbProduct.Items.Add(new ComboBoxItem { Content = p.Value, Tag = p.Key });
            cmbProduct.SelectedIndex = 0;
        }

        private async void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbProduct.SelectedItem is ComboBoxItem item)
            {
                _selectedProductId = item.Tag as int?;
                await RefreshDataAsync();
            }
        }

        // ════════════════════════════════════════════════════════════
        // GŁÓWNE ŁADOWANIE ZAMÓWIEŃ
        // ════════════════════════════════════════════════════════════
        private async Task RefreshDataAsync()
        {
            if (_isRefreshing) { _pendingRefresh = true; return; }
            _isRefreshing = true;
            _pendingRefresh = false;

            try
            {
                await LoadOrdersAsync();
                ClearDetails();
            }
            finally
            {
                _isRefreshing = false;
                if (_pendingRefresh) { _pendingRefresh = false; await RefreshDataAsync(); }
            }
        }

        private async Task LoadOrdersAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                string productFilter = _selectedProductId.HasValue ? "AND zmt.KodTowaru = @ProductId " : "";

                string sql = $@"
                    SELECT zm.Id, zm.KlientId,
                           SUM(ISNULL(zmt.Ilosc, 0)) AS IloscZamowiona,
                           SUM(ISNULL(CAST(zmt.Cena AS decimal(18,2)) * zmt.Ilosc, 0)) AS Wartosc,
                           zm.DataZamowienia, zm.DataUboju, zm.Status, zm.IdUser,
                           ISNULL(zm.CzyZafakturowane, 0) AS CzyZafakturowane,
                           zm.NumerFaktury,
                           zm.TransportKursID,
                           ISNULL(zm.CzyZmodyfikowaneDlaFaktur, 0) AS CzyZmodyfikowaneDlaFaktur,
                           zm.DataOstatniejModyfikacji,
                           zm.ModyfikowalPrzez,
                           ISNULL(zm.Waluta, 'PLN') AS Waluta,
                           CASE WHEN COUNT(zmt.Id) = 0 THEN 0
                                WHEN SUM(CASE WHEN zmt.Cena IS NULL OR zmt.Cena = '' OR CAST(zmt.Cena AS decimal(18,2)) = 0 THEN 1 ELSE 0 END) = 0 THEN 1
                                ELSE 0 END AS CzyMaCeny
                    FROM [dbo].[ZamowieniaMieso] zm
                    LEFT JOIN [dbo].[ZamowieniaMiesoTowar] zmt ON zm.Id = zmt.ZamowienieId
                    WHERE zm.DataUboju = @Day
                      AND zm.Status <> 'Anulowane'
                      {productFilter}
                    GROUP BY zm.Id, zm.KlientId, zm.DataZamowienia, zm.DataUboju, zm.Status, zm.IdUser,
                             zm.CzyZafakturowane, zm.NumerFaktury, zm.TransportKursID,
                             zm.CzyZmodyfikowaneDlaFaktur, zm.DataOstatniejModyfikacji, zm.ModyfikowalPrzez,
                             zm.Waluta
                    ORDER BY zm.Id";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@Day", _selectedDate.Date);
                if (_selectedProductId.HasValue)
                    cmd.Parameters.AddWithValue("@ProductId", _selectedProductId.Value);

                var kursIds = new HashSet<long>();
                var tempList = new List<ZamowienieInfo>();
                var seenIds = new HashSet<int>();

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    int id = reader.GetInt32(0);
                    if (!seenIds.Add(id)) continue;

                    int clientId = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    decimal ilosc = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                    decimal wartosc = reader.IsDBNull(3) ? 0 : reader.GetDecimal(3);
                    DateTime? dataZam = reader.IsDBNull(4) ? null : reader.GetDateTime(4);
                    DateTime? dataUboju = reader.IsDBNull(5) ? null : reader.GetDateTime(5);
                    string status = reader.IsDBNull(6) ? "" : reader.GetString(6);
                    string idUser = reader.IsDBNull(7) ? "" : reader.GetValue(7).ToString() ?? "";
                    bool czyZafakturowane = !reader.IsDBNull(8) && Convert.ToBoolean(reader.GetValue(8));
                    string numerFaktury = reader.IsDBNull(9) ? "" : reader.GetValue(9).ToString() ?? "";
                    long? transportKursId = reader.IsDBNull(10) ? null : Convert.ToInt64(reader.GetValue(10));
                    bool czyZmodyfikowane = !reader.IsDBNull(11) && Convert.ToBoolean(reader.GetValue(11));
                    DateTime? dataModyfikacji = reader.IsDBNull(12) ? null : reader.GetDateTime(12);
                    string modyfikowalPrzez = reader.IsDBNull(13) ? "" : reader.GetString(13);
                    string waluta = reader.IsDBNull(14) ? "PLN" : reader.GetString(14);
                    bool czyMaCeny = !reader.IsDBNull(15) && Convert.ToInt32(reader.GetValue(15)) == 1;

                    var (name, salesman) = _contractorsCache.TryGetValue(clientId, out var c) ? c : ($"Klient {clientId}", "");

                    var info = new ZamowienieInfo
                    {
                        Id = id, KlientId = clientId, Klient = name, Handlowiec = salesman,
                        TotalIlosc = ilosc, Wartosc = wartosc,
                        DataZamowienia = dataZam, DataUboju = dataUboju,
                        Status = status, UtworzonePrzez = idUser,
                        CzyZafakturowane = czyZafakturowane, NumerFaktury = numerFaktury,
                        TransportKursID = transportKursId,
                        CzyZmodyfikowaneDlaFaktur = czyZmodyfikowane,
                        DataOstatniejModyfikacji = dataModyfikacji,
                        ModyfikowalPrzez = modyfikowalPrzez, CzyMaCeny = czyMaCeny,
                        Waluta = waluta
                    };

                    if (transportKursId.HasValue) kursIds.Add(transportKursId.Value);
                    tempList.Add(info);
                }

                if (kursIds.Count > 0)
                {
                    var transportInfo = await LoadTransportInfoAsync(kursIds);
                    foreach (var info in tempList)
                    {
                        if (info.TransportKursID.HasValue && transportInfo.TryGetValue(info.TransportKursID.Value, out var ti))
                        {
                            info.GodzWyjazdu = ti.GodzWyjazdu;
                            info.Kierowca = ti.Kierowca;
                            info.Pojazd = ti.Pojazd;
                        }
                    }
                }

                // Załaduj avatary unikalnych handlowców (zanim zbudujemy VM, żeby HandlowiecAvatar miał dane)
                foreach (var h in tempList.Select(z => z.Handlowiec).Where(s => !string.IsNullOrEmpty(s)).Distinct())
                    EnsureHandlowiecAvatarCached(h);

                // Weryfikacja faktur (klient + suma kg) - PRZED budową VM, żeby ikony były od razu
                await VerifyInvoicesAsync(tempList);

                // BATCH update — zafakturowane zawsze pokazujemy (są na dole grupowania)
                var newList = new ObservableCollection<ZamowienieViewModel>();
                foreach (var info in tempList)
                {
                    newList.Add(new ZamowienieViewModel(info));
                }
                ZamowieniaList = newList;
                OnPropertyChanged(nameof(ZamowieniaList));

                // Wpinamy CollectionView z grupowaniem po statusie (priorytet: pilne na górze)
                var view = new System.Windows.Data.ListCollectionView(ZamowieniaList);
                view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription(nameof(ZamowienieViewModel.StatusDisplay)));
                view.CustomSort = Comparer<object>.Create((a, b) =>
                {
                    if (a is not ZamowienieViewModel x || b is not ZamowienieViewModel y) return 0;
                    int gx = StatusGroupOrder(x.StatusDisplay);
                    int gy = StatusGroupOrder(y.StatusDisplay);
                    if (gx != gy) return gx.CompareTo(gy);
                    return x.Info.Id.CompareTo(y.Info.Id);
                });
                view.Filter = OrdersFilter;
                _ordersView = view;
                dgOrders.ItemsSource = view;

                UpdateContextCounters(tempList);
                UpdateVerificationBar(tempList);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania zamówień: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Kolejność grup statusu (mniejszy = wyżej). Priorytet: zmiany wymagające reakcji,
        // potem nowe/w trakcie, na końcu zafakturowane.
        // "Zafakturowane ze zmianą" jest TUŻ NAD "Zafakturowane" — wymaga uwagi (zmiana post-faktura).
        // ════════════════════════════════════════════════════════════
        // WERYFIKACJA: dla każdego zamówienia z NumerFaktury sprawdza
        // czy w Symfonii istnieje taka faktura, czy klient się zgadza
        // i czy suma kg jest zbliżona. Wyniki ustawia bezpośrednio na ZamowienieInfo.
        // ════════════════════════════════════════════════════════════
        private async Task VerifyInvoicesAsync(List<ZamowienieInfo> orders)
        {
            // Wybierz tylko zamówienia z numerem faktury
            var nrFakturList = orders
                .Where(o => !string.IsNullOrWhiteSpace(o.NumerFaktury))
                .Select(o => o.NumerFaktury)
                .Distinct()
                .ToList();

            if (nrFakturList.Count == 0) return;

            // Mapa: NumerFaktury → (khid, nazwa klienta, suma kg) z Symfonii
            var dict = new Dictionary<string, (int Khid, string KhName, decimal Suma)>(StringComparer.OrdinalIgnoreCase);
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();

                // Parametryzacja po liście — buduję IN (...) z named params dla bezpieczeństwa
                var paramNames = nrFakturList.Select((_, i) => $"@k{i}").ToList();
                string sql = $@"SELECT dk.kod, dk.khid,
                                       ISNULL(kh.Name, '') AS KhName,
                                       COALESCE(SUM(dp.ilosc), 0) AS Suma
                                FROM HM.DK dk
                                LEFT JOIN HM.DP dp ON dp.super = dk.id
                                LEFT JOIN SSCommon.STContractors kh ON kh.Id = dk.khid
                                WHERE dk.kod IN ({string.Join(",", paramNames)})
                                  AND ISNULL(dk.anulowany, 0) = 0
                                GROUP BY dk.kod, dk.khid, kh.Name";

                await using var cmd = new SqlCommand(sql, cn);
                for (int i = 0; i < nrFakturList.Count; i++)
                    cmd.Parameters.AddWithValue(paramNames[i], nrFakturList[i]);

                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    string kod    = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    int khid      = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                    string khName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    decimal suma  = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3));
                    if (!string.IsNullOrEmpty(kod)) dict[kod] = (khid, khName, suma);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"VerifyInvoices error: {ex.Message}");
                return;  // bez weryfikacji — wszystkie zostają None
            }

            // Tolerancja porównania kg: 1 kg lub 0.5%
            const decimal QTY_TOL_KG = 1m;
            const decimal QTY_TOL_PCT = 0.005m;

            foreach (var o in orders)
            {
                if (string.IsNullOrWhiteSpace(o.NumerFaktury))
                {
                    o.MatchStatus = InvoiceMatchStatus.None;
                    continue;
                }
                if (!dict.TryGetValue(o.NumerFaktury, out var inv))
                {
                    o.MatchStatus = InvoiceMatchStatus.NotFound;
                    continue;
                }
                o.InvoiceKhId       = inv.Khid;
                o.InvoiceKhName     = inv.KhName;
                o.InvoiceTotalIlosc = inv.Suma;

                if (inv.Khid != o.KlientId)
                {
                    o.MatchStatus = InvoiceMatchStatus.ClientMismatch;
                    continue;
                }
                decimal diff = Math.Abs(inv.Suma - o.TotalIlosc);
                decimal allowed = Math.Max(QTY_TOL_KG, o.TotalIlosc * QTY_TOL_PCT);
                o.MatchStatus = diff <= allowed ? InvoiceMatchStatus.Ok : InvoiceMatchStatus.QtyMismatch;
            }
        }

        // ════════════════════════════════════════════════════════════
        // VERIFICATION BAR — KPI tile + klikalne chipy + filter
        // ════════════════════════════════════════════════════════════
        private void UpdateVerificationBar(List<ZamowienieInfo> orders)
        {
            int alarmCnt = orders.Count(o => o.MatchStatus == InvoiceMatchStatus.NotFound
                                           || o.MatchStatus == InvoiceMatchStatus.ClientMismatch);
            int warnCnt  = orders.Count(o => o.MatchStatus == InvoiceMatchStatus.QtyMismatch);
            int okCnt    = orders.Count(o => o.MatchStatus == InvoiceMatchStatus.Ok);
            int fakturCnt = alarmCnt + warnCnt + okCnt;  // wszystkie zafakturowane (z weryfikacją)

            txtAlarmCnt.Text = alarmCnt.ToString();
            txtWarnCnt.Text  = warnCnt.ToString();
            txtOkCnt.Text    = okCnt.ToString();

            chipAlarm.Visibility = alarmCnt > 0 ? Visibility.Visible : Visibility.Collapsed;
            chipWarn.Visibility  = warnCnt  > 0 ? Visibility.Visible : Visibility.Collapsed;
            chipOk.Visibility    = okCnt    > 0 ? Visibility.Visible : Visibility.Collapsed;

            // === KPI TILE (dzienne stats) ===
            // Liczymy ze WSZYSTKICH zamówień widocznych dla wybranego dnia (nie tylko zafakturowanych)
            var pl = CultureInfo.GetCultureInfo("pl-PL");
            decimal totalKg = orders.Sum(o => o.TotalIlosc);
            decimal totalPln = orders.Where(o => (o.Waluta ?? "PLN") != "EUR").Sum(o => o.Wartosc);
            decimal totalEur = orders.Where(o => o.Waluta == "EUR").Sum(o => o.Wartosc);

            txtKpiFakturCnt.Text = fakturCnt > 0 ? $"{fakturCnt}/{orders.Count}" : orders.Count.ToString();
            txtKpiProblems.Text  = (alarmCnt + warnCnt).ToString();
            // Kolor liczby problemów - czerwony gdy >0, szary gdy 0
            txtKpiProblems.Foreground = (alarmCnt + warnCnt) > 0
                ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
                : (Brush)FindResource("Brush.Text");
            // Waga: gdy >=1000 kg pokazuj jako tony z 1 miejscem dziesiętnym
            txtKpiWaga.Text = totalKg >= 1000m
                ? $"{(totalKg / 1000m).ToString("N1", pl)} t"
                : $"{totalKg.ToString("N0", pl)} kg";
            // Wartość: skróć jeśli >=10k (np. "280 k zł" / "1,2 mln zł")
            txtKpiWartosc.Text = FormatKpiAmount(totalPln, totalEur, pl);

            verifyBar.Visibility = orders.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Przycisk "Pokaż tylko niezgodne" - dostępny tylko gdy są niezgodne
            btnFilterMismatch.IsEnabled = (alarmCnt + warnCnt) > 0;

            // Wymuś reset filtra jeśli aktualnie aktywny tryb stał się pusty
            bool needReset = _filterMode switch
            {
                FilterMode.AlarmOnly    => alarmCnt == 0,
                FilterMode.WarnOnly     => warnCnt == 0,
                FilterMode.OkOnly       => okCnt == 0,
                FilterMode.MismatchAll  => (alarmCnt + warnCnt) == 0,
                _ => false
            };
            if (needReset)
            {
                _filterMode = FilterMode.None;
                _ordersView?.Refresh();
            }
            UpdateFilterChipsState();
        }

        private static string FormatKpiAmount(decimal pln, decimal eur, CultureInfo pl)
        {
            string main;
            if (pln >= 1_000_000m) main = $"{(pln / 1_000_000m).ToString("N2", pl)} mln zł";
            else if (pln >= 10_000m) main = $"{(pln / 1000m).ToString("N0", pl)} k zł";
            else main = $"{pln.ToString("N0", pl)} zł";

            if (eur > 0)
            {
                string eurStr = eur >= 10_000m
                    ? $"{(eur / 1000m).ToString("N0", pl)} k €"
                    : $"{eur.ToString("N0", pl)} €";
                main += $" + {eurStr}";
            }
            return main;
        }

        // Aktualizuje wizualny stan chipów (aktywny = ciemniejsze tło + biały tekst)
        // oraz przycisku/reset.
        private void UpdateFilterChipsState()
        {
            // Reset wszystkich chipów do "default"
            ResetChipStyle(chipAlarm, "#FEE2E2", "#DC2626");
            ResetChipStyle(chipWarn,  "#FFEDD5", "#EA580C");
            ResetChipStyle(chipOk,    "#DCFCE7", "#16A34A");

            switch (_filterMode)
            {
                case FilterMode.AlarmOnly:
                    SetChipActive(chipAlarm, "#DC2626");
                    break;
                case FilterMode.WarnOnly:
                    SetChipActive(chipWarn, "#EA580C");
                    break;
                case FilterMode.OkOnly:
                    SetChipActive(chipOk, "#16A34A");
                    break;
            }

            if (_filterMode == FilterMode.MismatchAll)
            {
                btnFilterMismatch.Content = "  ⚠ Filtruje niezgodne  ";
                btnFilterMismatch.Background = new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C));
                btnFilterMismatch.Foreground = Brushes.White;
            }
            else
            {
                btnFilterMismatch.Content = "  Pokaż tylko niezgodne  ";
                btnFilterMismatch.Background = new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7));
                btnFilterMismatch.Foreground = new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E));
            }

            btnFilterReset.Visibility = _filterMode != FilterMode.None
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ResetChipStyle(Border chip, string bgHex, string borderHex)
        {
            chip.Background = (Brush)new BrushConverter().ConvertFromString(bgHex)!;
            chip.BorderBrush = (Brush)new BrushConverter().ConvertFromString(borderHex)!;
            chip.BorderThickness = new Thickness(1);
            // Tekst w chip'ie zostaje w domyślnych kolorach (zdefiniowanych w XAML)
        }

        private void SetChipActive(Border chip, string solidHex)
        {
            chip.Background = (Brush)new BrushConverter().ConvertFromString(solidHex)!;
            chip.BorderBrush = (Brush)new BrushConverter().ConvertFromString(solidHex)!;
            chip.BorderThickness = new Thickness(2);
            // Zmień kolory wszystkich TextBlock w chipie na biały (mocniejsze podświetlenie)
            ColorChipText(chip, Brushes.White);
        }

        private static void ColorChipText(Border chip, Brush brush)
        {
            if (chip.Child is StackPanel sp)
            {
                foreach (var c in sp.Children)
                    if (c is TextBlock tb) tb.Foreground = brush;
            }
        }

        private bool OrdersFilter(object item)
        {
            if (_filterMode == FilterMode.None) return true;
            if (item is not ZamowienieViewModel vm) return false;
            var s = vm.Info.MatchStatus;
            return _filterMode switch
            {
                FilterMode.AlarmOnly   => s == InvoiceMatchStatus.NotFound || s == InvoiceMatchStatus.ClientMismatch,
                FilterMode.WarnOnly    => s == InvoiceMatchStatus.QtyMismatch,
                FilterMode.OkOnly      => s == InvoiceMatchStatus.Ok,
                FilterMode.MismatchAll => s == InvoiceMatchStatus.NotFound
                                       || s == InvoiceMatchStatus.ClientMismatch
                                       || s == InvoiceMatchStatus.QtyMismatch,
                _ => true
            };
        }

        private void ApplyFilterMode(FilterMode mode)
        {
            // Toggle - klik na ten sam chip wyłącza filtr
            _filterMode = (_filterMode == mode) ? FilterMode.None : mode;
            _ordersView?.Refresh();
            UpdateFilterChipsState();
        }

        private void ChipAlarm_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => ApplyFilterMode(FilterMode.AlarmOnly);
        private void ChipWarn_Click (object sender, System.Windows.Input.MouseButtonEventArgs e) => ApplyFilterMode(FilterMode.WarnOnly);
        private void ChipOk_Click   (object sender, System.Windows.Input.MouseButtonEventArgs e) => ApplyFilterMode(FilterMode.OkOnly);

        private void BtnFilterMismatch_Click(object sender, RoutedEventArgs e) => ApplyFilterMode(FilterMode.MismatchAll);

        private void BtnFilterReset_Click(object sender, RoutedEventArgs e)
        {
            _filterMode = FilterMode.None;
            _ordersView?.Refresh();
            UpdateFilterChipsState();
        }

        private static int StatusGroupOrder(string statusDisplay) => statusDisplay switch
        {
            "⚠ Do zatwierdzenia"                  => 0,
            "Nowe"                                 => 1,
            "W realizacji"                         => 2,
            "Zrealizowane"                         => 3,
            "Wydano"                               => 4,
            "⚠ Zafakturowane — zmiana po fakturze" => 8,
            "Zafakturowane"                        => 9,
            _                                      => 5
        };

        private void UpdateContextCounters(List<ZamowienieInfo> all)
        {
            int doFaktury = all.Count(z => !z.CzyZafakturowane && !z.CzyZmodyfikowaneDlaFaktur);
            int zafakt    = all.Count(z =>  z.CzyZafakturowane);
            int zmian     = all.Count(z =>  z.CzyZmodyfikowaneDlaFaktur);

            // Rozbicie kwoty na PLN i EUR (per Waluta zamówienia)
            var doFaktSrc = all.Where(z => !z.CzyZafakturowane);
            decimal kwotaPln = doFaktSrc.Where(z => (z.Waluta ?? "PLN") != "EUR").Sum(z => z.Wartosc);
            decimal kwotaEur = doFaktSrc.Where(z => z.Waluta == "EUR").Sum(z => z.Wartosc);

            txtCntDoFaktury.Text = $"{doFaktury} do faktury";
            txtCntZafakt.Text    = $"{zafakt} zafakt.";
            txtCntZmian.Text     = $"{zmian} zmian";
            txtCntKwota.Text     = $"{kwotaPln:N0} zł";

            // Pokaż badge EUR tylko gdy są zamówienia w euro
            if (kwotaEur > 0)
            {
                badgeKwotaEur.Visibility = Visibility.Visible;
                txtCntKwotaEur.Text = $"{kwotaEur:N0} €";
            }
            else
            {
                badgeKwotaEur.Visibility = Visibility.Collapsed;
            }
        }

        private async Task<Dictionary<long, (string GodzWyjazdu, string Kierowca, string Pojazd)>> LoadTransportInfoAsync(HashSet<long> kursIds)
        {
            var result = new Dictionary<long, (string GodzWyjazdu, string Kierowca, string Pojazd)>();
            if (kursIds.Count == 0) return result;

            string[] polskieMiesiace = { "", "Sty", "Lut", "Mar", "Kwi", "Maj", "Cze", "Lip", "Sie", "Wrz", "Paź", "Lis", "Gru" };

            try
            {
                await using var cn = new SqlConnection(_connTransport);
                await cn.OpenAsync();

                var kursIdsList = string.Join(",", kursIds);
                string sql = $@"
                    SELECT k.KursID, k.GodzWyjazdu, k.DataKursu,
                           ISNULL(kier.Imie + ' ' + kier.Nazwisko, '') AS Kierowca,
                           ISNULL(p.Rejestracja, '') AS Pojazd
                    FROM dbo.Kurs k
                    LEFT JOIN dbo.Kierowca kier ON k.KierowcaID = kier.KierowcaID
                    LEFT JOIN dbo.Pojazd p ON k.PojazdID = p.PojazdID
                    WHERE k.KursID IN ({kursIdsList})";

                await using var cmd = new SqlCommand(sql, cn);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    long kursId = reader.GetInt64(0);
                    TimeSpan? godz = reader.IsDBNull(1) ? null : reader.GetTimeSpan(1);
                    DateTime? data = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
                    string kierowca = reader.IsDBNull(3) ? "" : reader.GetString(3);
                    string pojazd   = reader.IsDBNull(4) ? "" : reader.GetString(4);

                    string godzStr = "";
                    if (godz.HasValue && data.HasValue)
                        godzStr = $"{godz.Value:hh\\:mm} {polskieMiesiace[data.Value.Month]} {data.Value.Day}";
                    else if (godz.HasValue)
                        godzStr = godz.Value.ToString(@"hh\:mm");

                    result[kursId] = (godzStr, kierowca, pojazd);
                }
            }
            catch { }
            return result;
        }

        private void ChkShowFakturowane_Changed(object sender, RoutedEventArgs e)
        {
            _showFakturowane = chkShowFakturowane.IsChecked == true;
            _ = RefreshDataAsync();
        }

        // ════════════════════════════════════════════════════════════
        // SZCZEGÓŁY ZAMÓWIENIA — z CancellationToken (race-safe)
        // ════════════════════════════════════════════════════════════
        private async void DgOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgOrders.SelectedItem is not ZamowienieViewModel vm)
            {
                ClearDetails();
                return;
            }

            _currentOrderId = vm.Info.Id;
            btnMarkFakturowane.IsEnabled = !vm.Info.CzyZafakturowane;
            btnCofnijFakturowanie.IsEnabled = vm.Info.CzyZafakturowane;

            // Cancel poprzednie pobranie szczegółów (race condition)
            _detailsCts?.Cancel();
            _detailsCts = new CancellationTokenSource();
            var ct = _detailsCts.Token;

            try
            {
                await LoadOrderDetailsAsync(vm, ct);
                if (ct.IsCancellationRequested) return;

                if (vm.Info.CzyZmodyfikowaneDlaFaktur)
                    await LoadChangesAsync(vm.Info.Id, ct);
                else
                    ShowNoChanges();

                if (ct.IsCancellationRequested) return;
                await LoadOrderUwagiAsync(vm.Info.Id, ct);

                if (ct.IsCancellationRequested) return;
                await LoadInvoiceItemsAsync(vm.Info.NumerFaktury, ct);

                if (ct.IsCancellationRequested) return;
                await LoadProductFlowAsync(vm, ct);
            }
            catch (OperationCanceledException) { }
        }

        // Pobiera kolumnę Uwagi z dbo.ZamowieniaMieso i wpisuje do txtNotatki
        private async Task LoadOrderUwagiAsync(int orderId, CancellationToken ct)
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync(ct);
                await using var cmd = new SqlCommand("SELECT ISNULL(Uwagi, '') FROM dbo.ZamowieniaMieso WHERE Id = @Id", cn);
                cmd.Parameters.AddWithValue("@Id", orderId);
                var uwagi = await cmd.ExecuteScalarAsync(ct);
                if (ct.IsCancellationRequested) return;
                _suppressNotatkiSave = true;
                txtNotatki.Text = uwagi?.ToString() ?? "";
                _suppressNotatkiSave = false;
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadOrderUwagi error: {ex.Message}"); }
        }

        // ════════════════════════════════════════════════════════════
        // POZYCJE FAKTURY — porównanie ZAM ↔ FAK side-by-side
        // Łączy pozycje z LibraNet.ZamowieniaMiesoTowar (po KodTowaru = HM.TW.id)
        // z pozycjami z Symfonia.HM.DP (po idtw = HM.TW.id), pokazuje różnice.
        // ════════════════════════════════════════════════════════════
        private async Task LoadInvoiceItemsAsync(string? numerFaktury, CancellationToken ct)
        {
            _invoiceCompareItems.Clear();

            // Reset wszystkich elementów UI tej zakładki
            invoiceHeader.Visibility = Visibility.Collapsed;
            invoiceSummary.Visibility = Visibility.Collapsed;
            txtInvoiceMatchTag.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(numerFaktury))
            {
                txtInvoiceHint.Text = "Zamówienie jeszcze nie zafakturowane";
                panelInvoiceEmpty.Visibility = Visibility.Visible;
                dgInvoiceItems.Visibility = Visibility.Collapsed;
                return;
            }

            // Klient + zamówienie aktualnie wybrane (do porównania)
            int orderId        = _currentOrderId ?? 0;
            int orderKlientId  = 0;
            decimal orderTotal = 0;
            if (dgOrders.SelectedItem is ZamowienieViewModel selVm)
            {
                orderKlientId = selVm.Info.KlientId;
                orderTotal    = selVm.Info.TotalIlosc;
            }

            int invKhid = 0;
            string invKhName = "";
            var invoicePositions = new Dictionary<int, (decimal Qty, decimal Cena, decimal Wartosc)>();
            var orderPositions   = new Dictionary<int, decimal>();

            try
            {
                // 1) FAKTURA z .112 — header + pozycje
                await using (var cnH = new SqlConnection(_connHandel))
                {
                    await cnH.OpenAsync(ct);

                    const string headerSql = @"SELECT TOP 1 dk.khid, ISNULL(kh.Name, '') AS KhName
                                               FROM HM.DK dk
                                               LEFT JOIN SSCommon.STContractors kh ON kh.Id = dk.khid
                                               WHERE dk.kod = @kod AND ISNULL(dk.anulowany, 0) = 0";
                    await using (var cmdHdr = new SqlCommand(headerSql, cnH))
                    {
                        cmdHdr.Parameters.AddWithValue("@kod", numerFaktury);
                        await using var rdrHdr = await cmdHdr.ExecuteReaderAsync(ct);
                        if (await rdrHdr.ReadAsync(ct))
                        {
                            invKhid   = rdrHdr.IsDBNull(0) ? 0 : rdrHdr.GetInt32(0);
                            invKhName = rdrHdr.IsDBNull(1) ? "" : rdrHdr.GetString(1);
                        }
                    }

                    const string posSql = @"SELECT dp.idtw, dp.ilosc, dp.cena, dp.wartNetto
                                            FROM HM.DP dp
                                            INNER JOIN HM.DK dk ON dk.id = dp.super
                                            WHERE dk.kod = @kod
                                            ORDER BY dp.lp";
                    await using var cmdP = new SqlCommand(posSql, cnH);
                    cmdP.Parameters.AddWithValue("@kod", numerFaktury);
                    await using var rdrP = await cmdP.ExecuteReaderAsync(ct);
                    while (await rdrP.ReadAsync(ct))
                    {
                        int twId      = rdrP.GetInt32(0);
                        decimal ilosc = rdrP.IsDBNull(1) ? 0 : Convert.ToDecimal(rdrP.GetValue(1));
                        decimal cena  = rdrP.IsDBNull(2) ? 0 : Convert.ToDecimal(rdrP.GetValue(2));
                        decimal wart  = rdrP.IsDBNull(3) ? 0 : Convert.ToDecimal(rdrP.GetValue(3));
                        // Jeśli ten sam towar występuje w wielu pozycjach faktury (rzadko, ale zdarza się) - sumuję
                        if (invoicePositions.TryGetValue(twId, out var prev))
                            invoicePositions[twId] = (prev.Qty + ilosc, cena, prev.Wartosc + wart);
                        else
                            invoicePositions[twId] = (ilosc, cena, wart);
                    }
                }

                if (ct.IsCancellationRequested) return;

                // 2) ZAMÓWIENIE z .109 — pozycje (prosty SUM żeby zsumować ewentualne duplikaty)
                if (orderId > 0)
                {
                    await using var cnL = new SqlConnection(_connLibra);
                    await cnL.OpenAsync(ct);
                    const string ordSql = @"SELECT KodTowaru, SUM(ISNULL(Ilosc, 0)) AS Ilosc
                                            FROM [dbo].[ZamowieniaMiesoTowar]
                                            WHERE ZamowienieId = @OrderId
                                            GROUP BY KodTowaru";
                    await using var cmdO = new SqlCommand(ordSql, cnL);
                    cmdO.Parameters.AddWithValue("@OrderId", orderId);
                    await using var rdrO = await cmdO.ExecuteReaderAsync(ct);
                    while (await rdrO.ReadAsync(ct))
                    {
                        int twId = rdrO.GetInt32(0);
                        decimal qty = rdrO.IsDBNull(1) ? 0 : rdrO.GetDecimal(1);
                        orderPositions[twId] = qty;
                    }
                }

                if (ct.IsCancellationRequested) return;

                // Faktura nie istnieje w Symfonii
                if (invoicePositions.Count == 0 && invKhid == 0)
                {
                    txtInvoiceHint.Text = $"Nie znaleziono faktury {numerFaktury} w Symfonii";
                    panelInvoiceEmpty.Visibility = Visibility.Visible;
                    dgInvoiceItems.Visibility = Visibility.Collapsed;
                    return;
                }

                // 3) MERGE - dla każdego unikalnego towaru z OBYDWU stron tworzę wiersz
                var allTwIds = new HashSet<int>(invoicePositions.Keys);
                allTwIds.UnionWith(orderPositions.Keys);
                const decimal QTY_TOL = 0.05m;  // 50g tolerancji per pozycja

                decimal sumOrderKg = 0, sumInvoiceKg = 0, sumInvoiceVal = 0;
                foreach (var twId in allTwIds)
                {
                    bool hasOrder   = orderPositions.TryGetValue(twId, out var ordQty);
                    bool hasInvoice = invoicePositions.TryGetValue(twId, out var invPos);

                    PositionCompareStatus stat;
                    if (hasOrder && hasInvoice)
                    {
                        stat = Math.Abs(invPos.Qty - ordQty) <= QTY_TOL
                            ? PositionCompareStatus.Match
                            : PositionCompareStatus.QtyDiff;
                    }
                    else if (hasInvoice)
                    {
                        stat = PositionCompareStatus.OnlyOnInvoice;
                    }
                    else
                    {
                        stat = PositionCompareStatus.OnlyOnOrder;
                    }

                    string nazwa = _productCodeCache.TryGetValue(twId, out var c) ? c
                                 : _productNameCache.TryGetValue(twId, out var n) ? n
                                 : $"Towar {twId}";

                    var item = new InvoicePositionCompare
                    {
                        Image      = _productImagesCache.TryGetValue(twId, out var img) ? img : null,
                        Produkt    = nazwa,
                        OrderQty   = hasOrder   ? ordQty       : null,
                        InvoiceQty = hasInvoice ? invPos.Qty   : null,
                        Cena       = hasInvoice ? invPos.Cena  : null,
                        Wartosc    = hasInvoice ? invPos.Wartosc : null,
                        Status     = stat
                    };
                    _invoiceCompareItems.Add(item);

                    if (hasOrder)   sumOrderKg   += ordQty;
                    if (hasInvoice) { sumInvoiceKg += invPos.Qty; sumInvoiceVal += invPos.Wartosc; }
                }

                // 4) Sortowanie: najpierw alarmy, potem ostrzeżenia, na koniec zgodne
                var sorted = _invoiceCompareItems
                    .OrderBy(i => i.Status switch
                    {
                        PositionCompareStatus.OnlyOnOrder   => 0,
                        PositionCompareStatus.OnlyOnInvoice => 1,
                        PositionCompareStatus.QtyDiff       => 2,
                        _                                    => 3
                    })
                    .ThenBy(i => i.Produkt)
                    .ToList();
                _invoiceCompareItems.Clear();
                foreach (var s in sorted) _invoiceCompareItems.Add(s);

                // 5) Header
                txtInvoiceNumer.Text  = numerFaktury;
                txtInvoiceKlient.Text = string.IsNullOrEmpty(invKhName) ? $"khid={invKhid}" : invKhName;
                if (orderKlientId > 0 && invKhid > 0)
                {
                    bool zgodny = invKhid == orderKlientId;
                    txtInvoiceMatchTag.Text = zgodny ? "✓ ZGODNY" : "✖ NIEZGODNY Z ZAMÓWIENIEM";
                    txtInvoiceMatchTag.Foreground = Brushes.White;
                    txtInvoiceMatchTag.Background = zgodny
                        ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))
                        : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                    txtInvoiceMatchTag.Visibility = Visibility.Visible;
                    txtInvoiceKlient.Foreground = zgodny
                        ? (Brush)FindResource("Brush.Text")
                        : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
                }
                invoiceHeader.Visibility = Visibility.Visible;

                // 6) Summary stats
                var pl = CultureInfo.GetCultureInfo("pl-PL");
                txtSumOrderKg.Text   = sumOrderKg.ToString("N2", pl);
                txtSumInvoiceKg.Text = sumInvoiceKg.ToString("N2", pl);
                txtSumInvoiceVal.Text = sumInvoiceVal > 0 ? $"({sumInvoiceVal.ToString("N2", pl)} zł)" : "";
                txtSumOrderVal.Text   = "";   // wartość zamówienia liczymy tylko z faktury, dla zam mieszanego nie mamy łatwo

                decimal diffKg = sumInvoiceKg - sumOrderKg;
                bool diffOk = Math.Abs(diffKg) <= 0.5m;
                txtDiffValue.Text = (diffKg >= 0 ? "+" : "") + diffKg.ToString("N2", pl) + " kg";
                txtDiffIcon.Text  = diffOk ? "✓" : "⚠";
                txtDiffLabel.Text = diffOk ? "Zgodne" : "Różnica";
                diffBadge.Background = diffOk
                    ? new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A))    // zielony
                    : new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C));  // pomarańczowy
                invoiceSummary.Visibility = Visibility.Visible;

                // 7) Grid
                panelInvoiceEmpty.Visibility = Visibility.Collapsed;
                dgInvoiceItems.Visibility = Visibility.Visible;
                SetupInvoiceCompareGrid();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadInvoiceItems error: {ex.Message}");
                if (!ct.IsCancellationRequested)
                {
                    txtInvoiceHint.Text = $"Błąd odczytu faktury: {ex.Message}";
                    panelInvoiceEmpty.Visibility = Visibility.Visible;
                    dgInvoiceItems.Visibility = Visibility.Collapsed;
                    invoiceHeader.Visibility = Visibility.Collapsed;
                    invoiceSummary.Visibility = Visibility.Collapsed;
                }
            }
        }

        // Buduje kolumny porównawcze: Produkt | Zam.kg | Faktura.kg | Δ | Cena | Wartość | Status
        // Wiersze są kolorowane zależnie od statusu (RowBg).
        private void SetupInvoiceCompareGrid()
        {
            dgInvoiceItems.ItemsSource = _invoiceCompareItems;
            dgInvoiceItems.Columns.Clear();

            // Row style — kolorowanie tła wg statusu
            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty,
                new System.Windows.Data.Binding(nameof(InvoicePositionCompare.RowBg))));
            dgInvoiceItems.RowStyle = rowStyle;

            var rightStyle = new Style(typeof(TextBlock));
            rightStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            rightStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            rightStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0)));

            // PRODUKT (image + nazwa, jak w zakładce zamówienia)
            var spFactory = new FrameworkElementFactory(typeof(StackPanel));
            spFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            spFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            var borderImgFactory = new FrameworkElementFactory(typeof(Border));
            borderImgFactory.SetValue(Border.WidthProperty, 32.0);
            borderImgFactory.SetValue(Border.HeightProperty, 32.0);
            borderImgFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderImgFactory.SetValue(Border.BorderBrushProperty, (Brush)FindResource("Brush.Border"));
            borderImgFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderImgFactory.SetValue(Border.BackgroundProperty, Brushes.White);
            borderImgFactory.SetValue(Border.MarginProperty, new Thickness(0, 0, 8, 0));
            borderImgFactory.SetValue(Border.ClipToBoundsProperty, true);
            var imgFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
            imgFactory.SetBinding(System.Windows.Controls.Image.SourceProperty,
                new System.Windows.Data.Binding(nameof(InvoicePositionCompare.Image)));
            imgFactory.SetValue(System.Windows.Controls.Image.StretchProperty, Stretch.Uniform);
            imgFactory.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
            borderImgFactory.AppendChild(imgFactory);
            spFactory.AppendChild(borderImgFactory);

            var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
            tbFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(InvoicePositionCompare.Produkt)));
            tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            tbFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            tbFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            spFactory.AppendChild(tbFactory);

            dgInvoiceItems.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Produkt",
                CellTemplate = new DataTemplate { VisualTree = spFactory },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 90
            });

            // Zam. kg
            var orderQtyCol = new DataGridTextColumn
            {
                Header = "Zam. kg",
                Binding = new System.Windows.Data.Binding(nameof(InvoicePositionCompare.OrderQtyStr)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                MinWidth = 85
            };
            var orderQtyStyle = new Style(typeof(TextBlock));
            orderQtyStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            orderQtyStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            orderQtyStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0)));
            orderQtyStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new System.Windows.Data.Binding(nameof(InvoicePositionCompare.OrderQtyBrush))));
            orderQtyCol.ElementStyle = orderQtyStyle;
            dgInvoiceItems.Columns.Add(orderQtyCol);

            // Faktura kg
            var invQtyCol = new DataGridTextColumn
            {
                Header = "Faktura kg",
                Binding = new System.Windows.Data.Binding(nameof(InvoicePositionCompare.InvoiceQtyStr)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                MinWidth = 95
            };
            var invQtyStyle = new Style(typeof(TextBlock));
            invQtyStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            invQtyStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            invQtyStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0)));
            invQtyStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
            invQtyStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new System.Windows.Data.Binding(nameof(InvoicePositionCompare.InvoiceQtyBrush))));
            invQtyCol.ElementStyle = invQtyStyle;
            dgInvoiceItems.Columns.Add(invQtyCol);

            // Δ (różnica)
            var diffCol = new DataGridTextColumn
            {
                Header = "Δ",
                Binding = new System.Windows.Data.Binding(nameof(InvoicePositionCompare.DiffStr)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                MinWidth = 80
            };
            var diffStyle = new Style(typeof(TextBlock));
            diffStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            diffStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            diffStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0)));
            diffStyle.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold));
            diffStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,
                new System.Windows.Data.Binding(nameof(InvoicePositionCompare.DiffBrush))));
            diffCol.ElementStyle = diffStyle;
            dgInvoiceItems.Columns.Add(diffCol);

            // Cena
            dgInvoiceItems.Columns.Add(new DataGridTextColumn
            {
                Header = "Cena",
                Binding = new System.Windows.Data.Binding(nameof(InvoicePositionCompare.CenaStr)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                MinWidth = 75,
                ElementStyle = rightStyle
            });

            // Wartość
            dgInvoiceItems.Columns.Add(new DataGridTextColumn
            {
                Header = "Wartość zł",
                Binding = new System.Windows.Data.Binding(nameof(InvoicePositionCompare.WartoscStr)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                MinWidth = 100,
                ElementStyle = rightStyle
            });

            // Status (ikona + tekst)
            var statusSp = new FrameworkElementFactory(typeof(StackPanel));
            statusSp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            statusSp.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            var statusIcon = new FrameworkElementFactory(typeof(TextBlock));
            statusIcon.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(InvoicePositionCompare.StatusIcon)));
            statusIcon.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(InvoicePositionCompare.DiffBrush)));
            statusIcon.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            statusIcon.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 6, 0));
            statusSp.AppendChild(statusIcon);
            var statusText = new FrameworkElementFactory(typeof(TextBlock));
            statusText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(InvoicePositionCompare.StatusText)));
            statusText.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(nameof(InvoicePositionCompare.DiffBrush)));
            statusText.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            statusSp.AppendChild(statusText);

            dgInvoiceItems.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Status",
                CellTemplate = new DataTemplate { VisualTree = statusSp },
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                MinWidth = 130
            });
        }

        // ════════════════════════════════════════════════════════════
        // ZAKŁADKA "Zam / Wyd / Fak" — przepływ produktu w kg
        //   Zamówiono   = dbo.ZamowieniaMiesoTowar.Ilosc
        //   Wydano      = dbo.ZamowienieWydanieRoznice (różnice z dialogu magazyniera);
        //                 brak wpisu + Status='Wydany'/'Wydano' => wydano = zamówiono;
        //                 zamówienie niewydane => 0
        //   Zafakturowano = HM.DP po NumerFaktury (Symfonia)
        // ════════════════════════════════════════════════════════════
        private async Task LoadProductFlowAsync(ZamowienieViewModel vm, CancellationToken ct)
        {
            // Defensywnie: ItemsSource musi wskazywać na _flowItems, inaczej Add() nic nie wyrenderuje.
            if (!ReferenceEquals(dgFlowItems.ItemsSource, _flowItems))
                dgFlowItems.ItemsSource = _flowItems;
            _flowItems.Clear();

            int orderId = vm.Info.Id;
            string? numerFaktury = vm.Info.NumerFaktury;
            bool czyWydane = vm.Info.Status == "Wydany" || vm.Info.Status == "Wydano";

            var zamowiono = new Dictionary<int, decimal>();   // TowarId -> kg
            var wydano    = new Dictionary<int, decimal>();
            var fakturowano = new Dictionary<int, decimal>();

            try
            {
                // 1) ZAMÓWIONO + WYDANO z LibraNet
                await using (var cnL = new SqlConnection(_connLibra))
                {
                    await cnL.OpenAsync(ct);

                    const string ordSql = @"SELECT KodTowaru, SUM(ISNULL(Ilosc, 0)) AS Ilosc
                                            FROM [dbo].[ZamowieniaMiesoTowar]
                                            WHERE ZamowienieId = @OrderId
                                            GROUP BY KodTowaru";
                    await using (var cmd = new SqlCommand(ordSql, cnL))
                    {
                        cmd.Parameters.AddWithValue("@OrderId", orderId);
                        await using var rd = await cmd.ExecuteReaderAsync(ct);
                        while (await rd.ReadAsync(ct))
                        {
                            int twId = rd.GetInt32(0);
                            decimal qty = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                            zamowiono[twId] = qty;
                        }
                    }

                    if (ct.IsCancellationRequested) return;

                    // Wydano: per pozycja z ZamowienieWydanieRoznice (jeśli tabela istnieje).
                    if (czyWydane)
                    {
                        var roznice = new Dictionary<int, decimal>();
                        await using (var checkCmd = new SqlCommand(
                            "SELECT COUNT(*) FROM sys.objects WHERE name='ZamowienieWydanieRoznice' AND type='U'", cnL))
                        {
                            int exists = (int)(await checkCmd.ExecuteScalarAsync(ct) ?? 0);
                            if (exists > 0)
                            {
                                await using var cmd = new SqlCommand(
                                    @"SELECT KodTowaru, IloscWydana
                                      FROM dbo.ZamowienieWydanieRoznice
                                      WHERE ZamowienieId = @Id", cnL);
                                cmd.Parameters.AddWithValue("@Id", orderId);
                                await using var rd = await cmd.ExecuteReaderAsync(ct);
                                while (await rd.ReadAsync(ct))
                                    roznice[rd.GetInt32(0)] = Convert.ToDecimal(rd.GetValue(1));
                            }
                        }

                        foreach (var (twId, ilZam) in zamowiono)
                            wydano[twId] = roznice.TryGetValue(twId, out var w) ? w : ilZam;
                    }
                }

                if (ct.IsCancellationRequested) return;

                // 2) ZAFAKTUROWANO z Symfonii (HM.DP po numerze faktury)
                if (!string.IsNullOrWhiteSpace(numerFaktury))
                {
                    await using var cnH = new SqlConnection(_connHandel);
                    await cnH.OpenAsync(ct);
                    const string posSql = @"SELECT dp.idtw, SUM(ISNULL(dp.ilosc, 0)) AS Ilosc
                                            FROM HM.DP dp
                                            INNER JOIN HM.DK dk ON dk.id = dp.super
                                            WHERE dk.kod = @kod AND ISNULL(dk.anulowany, 0) = 0
                                            GROUP BY dp.idtw";
                    await using var cmd = new SqlCommand(posSql, cnH);
                    cmd.Parameters.AddWithValue("@kod", numerFaktury);
                    await using var rd = await cmd.ExecuteReaderAsync(ct);
                    while (await rd.ReadAsync(ct))
                    {
                        int twId = rd.GetInt32(0);
                        decimal qty = rd.IsDBNull(1) ? 0m : Convert.ToDecimal(rd.GetValue(1));
                        fakturowano[twId] = qty;
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadProductFlow error: {ex.Message}");
                if (!ct.IsCancellationRequested)
                {
                    txtFlowHint.Text = $"Błąd odczytu: {ex.Message}";
                    panelFlowEmpty.Visibility = Visibility.Visible;
                    dgFlowItems.Visibility = Visibility.Collapsed;
                    flowSummary.Visibility = Visibility.Collapsed;
                }
                return;
            }

            if (ct.IsCancellationRequested) return;

            // 3) MERGE wszystkich towarów z 3 źródeł
            var allTwIds = new HashSet<int>(zamowiono.Keys);
            allTwIds.UnionWith(wydano.Keys);
            allTwIds.UnionWith(fakturowano.Keys);

            if (allTwIds.Count == 0)
            {
                txtFlowHint.Text = "Brak pozycji w zamówieniu";
                panelFlowEmpty.Visibility = Visibility.Visible;
                dgFlowItems.Visibility = Visibility.Collapsed;
                flowSummary.Visibility = Visibility.Collapsed;
                return;
            }

            decimal sumZ = 0, sumW = 0, sumF = 0;
            foreach (var twId in allTwIds)
            {
                bool hasZ = zamowiono.TryGetValue(twId, out var z);
                bool hasW = wydano.TryGetValue(twId, out var w);
                bool hasF = fakturowano.TryGetValue(twId, out var f);

                string nazwa = _productCodeCache.TryGetValue(twId, out var c) ? c
                             : _productNameCache.TryGetValue(twId, out var n) ? n
                             : $"Towar {twId}";

                _flowItems.Add(new ProductFlowItem
                {
                    Image = _productImagesCache.TryGetValue(twId, out var img) ? img : null,
                    Produkt = nazwa,
                    Zamowiono = hasZ ? z : (decimal?)null,
                    Wydano = czyWydane ? (hasW ? w : (decimal?)null) : (decimal?)null,
                    Zafakturowano = !string.IsNullOrWhiteSpace(numerFaktury) ? (hasF ? f : (decimal?)null) : (decimal?)null,
                });

                if (hasZ) sumZ += z;
                if (hasW) sumW += w;
                if (hasF) sumF += f;
            }

            // Sortowanie: pozycje z różnicami pierwsze, potem zgodne, potem alfabetycznie
            var sorted = _flowItems
                .OrderByDescending(i => i.HasAnyDiff ? 1 : 0)
                .ThenBy(i => i.Produkt)
                .ToList();
            _flowItems.Clear();
            foreach (var s in sorted) _flowItems.Add(s);

            // 4) Summary
            var pl = CultureInfo.GetCultureInfo("pl-PL");
            txtFlowSumZam.Text = sumZ.ToString("N2", pl);
            txtFlowSumWyd.Text = czyWydane ? sumW.ToString("N2", pl) : "—";
            txtFlowSumFak.Text = !string.IsNullOrWhiteSpace(numerFaktury) ? sumF.ToString("N2", pl) : "—";

            decimal dWZ = sumW - sumZ;
            decimal dFW = sumF - sumW;
            txtFlowDeltaWydZam.Text = czyWydane ? FormatDelta(dWZ, pl) : "";
            txtFlowDeltaWydZam.Foreground = DeltaBrush(dWZ);
            txtFlowDeltaFakWyd.Text = (czyWydane && !string.IsNullOrWhiteSpace(numerFaktury)) ? FormatDelta(dFW, pl) : "";
            txtFlowDeltaFakWyd.Foreground = DeltaBrush(dFW);

            flowSummary.Visibility = Visibility.Visible;
            panelFlowEmpty.Visibility = Visibility.Collapsed;
            dgFlowItems.Visibility = Visibility.Visible;
        }

        private static string FormatDelta(decimal v, CultureInfo pl)
        {
            if (Math.Abs(v) < 0.005m) return "✓ 0,00 kg";
            return (v >= 0 ? "+" : "") + v.ToString("N2", pl) + " kg";
        }

        private static Brush DeltaBrush(decimal v)
        {
            if (Math.Abs(v) < 0.005m) return Brushes.Gray;
            return v < 0
                ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))   // brak — czerwony
                : new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C));  // nadwyżka — pomarańczowy
        }

        // Kolumny: Produkt | Zam | Wyd | Fak | Δ Wyd-Zam | Δ Fak-Wyd
        private void SetupFlowGrid()
        {
            dgFlowItems.ItemsSource = _flowItems;
            dgFlowItems.Columns.Clear();

            var rowStyle = new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty,
                new System.Windows.Data.Binding(nameof(ProductFlowItem.RowBg))));
            dgFlowItems.RowStyle = rowStyle;

            // Produkt (image + nazwa)
            var spFactory = new FrameworkElementFactory(typeof(StackPanel));
            spFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            spFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            var borderImgFactory = new FrameworkElementFactory(typeof(Border));
            borderImgFactory.SetValue(Border.WidthProperty, 32.0);
            borderImgFactory.SetValue(Border.HeightProperty, 32.0);
            borderImgFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderImgFactory.SetValue(Border.BorderBrushProperty, (Brush)FindResource("Brush.Border"));
            borderImgFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderImgFactory.SetValue(Border.BackgroundProperty, Brushes.White);
            borderImgFactory.SetValue(Border.MarginProperty, new Thickness(0, 0, 8, 0));
            borderImgFactory.SetValue(Border.ClipToBoundsProperty, true);
            var imgFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
            imgFactory.SetBinding(System.Windows.Controls.Image.SourceProperty,
                new System.Windows.Data.Binding(nameof(ProductFlowItem.Image)));
            imgFactory.SetValue(System.Windows.Controls.Image.StretchProperty, Stretch.Uniform);
            imgFactory.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
            borderImgFactory.AppendChild(imgFactory);
            spFactory.AppendChild(borderImgFactory);

            var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
            tbFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ProductFlowItem.Produkt)));
            tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            tbFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            tbFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            spFactory.AppendChild(tbFactory);

            dgFlowItems.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Produkt",
                CellTemplate = new DataTemplate { VisualTree = spFactory },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 90
            });

            Style RightStyle(string brushProp = null!, FontWeight? weight = null)
            {
                var s = new Style(typeof(TextBlock));
                s.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
                s.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
                s.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(0, 0, 8, 0)));
                if (weight.HasValue) s.Setters.Add(new Setter(TextBlock.FontWeightProperty, weight.Value));
                if (brushProp != null)
                    s.Setters.Add(new Setter(TextBlock.ForegroundProperty, new System.Windows.Data.Binding(brushProp)));
                return s;
            }

            dgFlowItems.Columns.Add(new DataGridTextColumn
            {
                Header = "Zam. kg",
                Binding = new System.Windows.Data.Binding(nameof(ProductFlowItem.ZamowionoStr)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                MinWidth = 85,
                ElementStyle = RightStyle()
            });

            dgFlowItems.Columns.Add(new DataGridTextColumn
            {
                Header = "Wyd. kg",
                Binding = new System.Windows.Data.Binding(nameof(ProductFlowItem.WydanoStr)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                MinWidth = 85,
                ElementStyle = RightStyle(nameof(ProductFlowItem.WydanoBrush), FontWeights.SemiBold)
            });

            dgFlowItems.Columns.Add(new DataGridTextColumn
            {
                Header = "Fak. kg",
                Binding = new System.Windows.Data.Binding(nameof(ProductFlowItem.ZafakturowanoStr)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                MinWidth = 85,
                ElementStyle = RightStyle(nameof(ProductFlowItem.ZafakturowanoBrush), FontWeights.SemiBold)
            });
        }

        private bool _suppressNotatkiSave = false;
        // Notatki sa tylko-do-odczytu — TxtNotatki_LostFocus zostal usuniety,
        // ale zachowujemy _suppressNotatkiSave bo LoadOrderUwagiAsync go uzywa
        // (na wypadek gdyby ktos w przyszlosci dodal edycje, _suppressNotatkiSave zapobiega saveowaniu podczas LOAD).

        private void ShowNoChanges()
        {
            panelNoChanges.Visibility = Visibility.Visible;
            panelChangesDiff.Visibility = Visibility.Collapsed;
            badgeChanges.Visibility = Visibility.Collapsed;
            tabZmiany.Tag = null;   // wyłącz pomarańczowe podświetlenie
        }

        private async Task LoadOrderDetailsAsync(ZamowienieViewModel vm, CancellationToken ct)
        {
            _dtDetails.Clear();
            _dtDetails.Columns.Clear();
            _dtDetails.Columns.Add("Image",   typeof(BitmapImage));   // miniaturka towaru (nullable)
            _dtDetails.Columns.Add("Produkt", typeof(string));
            _dtDetails.Columns.Add("Ilosc",   typeof(decimal));
            _dtDetails.Columns.Add("Cena",    typeof(string));
            _dtDetails.Columns.Add("Wartosc", typeof(decimal));

            // Info wybranego zamówienia w toolbarze (paneOrderInfo zawsze visible — tylko teksty się zmieniają)
            txtOdbiorca.Text = vm.Info.Klient;

            // Meta jako single-line: "Handlowiec · 18.03.2026"
            var metaParts = new List<string>();
            if (!string.IsNullOrEmpty(vm.Info.Handlowiec)) metaParts.Add(vm.Info.Handlowiec);
            if (vm.Info.DataZamowienia.HasValue) metaParts.Add(vm.Info.DataZamowienia.Value.ToString("dd.MM.yyyy"));
            txtOrderMeta.Text = string.Join("  ·  ", metaParts);

            if (!string.IsNullOrEmpty(vm.Info.GodzWyjazdu) || !string.IsNullOrEmpty(vm.Info.Kierowca))
            {
                borderTransport.Visibility = Visibility.Visible;
                txtGodzWyjazdu.Text = vm.Info.GodzWyjazdu ?? "";
                txtKierowca.Text = vm.Info.Kierowca ?? "";
                txtPojazd.Text = vm.Info.Pojazd ?? "";
            }
            else
            {
                borderTransport.Visibility = Visibility.Collapsed;
            }

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync(ct);

                const string sql = "SELECT KodTowaru, Ilosc, Cena FROM [dbo].[ZamowieniaMiesoTowar] WHERE ZamowienieId = @OrderId";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@OrderId", vm.Info.Id);
                await using var reader = await cmd.ExecuteReaderAsync(ct);

                while (await reader.ReadAsync(ct))
                {
                    int kod = reader.GetInt32(0);
                    decimal ilosc = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                    string cena = reader.IsDBNull(2) ? "" : reader.GetString(2);

                    decimal cenaNum = 0;
                    decimal.TryParse(cena.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out cenaNum);

                    // Nazwa produktu = kolumna 'kod' z HM.TW (jak w oknie nowego zamówienia)
                    string nazwa = _productCodeCache.TryGetValue(kod, out var c) ? c
                                 : _productNameCache.TryGetValue(kod, out var n) ? n
                                 : $"Towar {kod}";

                    var row = _dtDetails.NewRow();
                    row["Image"]   = _productImagesCache.TryGetValue(kod, out var img) ? (object)img : DBNull.Value;
                    row["Produkt"] = nazwa;
                    row["Ilosc"]   = ilosc;
                    row["Cena"]    = cena;
                    row["Wartosc"] = ilosc * cenaNum;
                    _dtDetails.Rows.Add(row);
                }

                if (ct.IsCancellationRequested) return;
                _currentWalutaSymbol = vm.WalutaSymbol;   // synchronizuj nagłówki kolumn z walutą zamówienia
                SetupDetailsGrid();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    MessageBox.Show($"Błąd ładowania pozycji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string _currentWalutaSymbol = "zł";

        private void SetupDetailsGrid()
        {
            SetupItemsGridColumns(dgDetails, _dtDetails, _currentWalutaSymbol);
        }

        // Wspolny builder kolumn dla "Pozycje zamowienia" i "Pozycje faktury".
        // Wymaga zeby DataTable mial kolumny: Image, Produkt, Ilosc, Cena, Wartosc.
        private void SetupItemsGridColumns(DataGrid dg, DataTable source, string walutaSymbol)
        {
            dg.ItemsSource = source.DefaultView;
            dg.Columns.Clear();

            var rightStyle = new Style(typeof(TextBlock));
            rightStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            rightStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            rightStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));

            // Kolumna PRODUKT — image + nazwa razem (auto-wrap dla długich nazw)
            var spFactory = new FrameworkElementFactory(typeof(StackPanel));
            spFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            spFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            var borderImgFactory = new FrameworkElementFactory(typeof(Border));
            borderImgFactory.SetValue(Border.WidthProperty, 32.0);
            borderImgFactory.SetValue(Border.HeightProperty, 32.0);
            borderImgFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderImgFactory.SetValue(Border.BorderBrushProperty, (Brush)FindResource("Brush.Border"));
            borderImgFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            borderImgFactory.SetValue(Border.BackgroundProperty, Brushes.White);
            borderImgFactory.SetValue(Border.MarginProperty, new Thickness(0, 0, 8, 0));
            borderImgFactory.SetValue(Border.ClipToBoundsProperty, true);

            var imgFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Image));
            imgFactory.SetBinding(System.Windows.Controls.Image.SourceProperty, new System.Windows.Data.Binding("Image"));
            imgFactory.SetValue(System.Windows.Controls.Image.StretchProperty, Stretch.Uniform);
            imgFactory.SetValue(RenderOptions.BitmapScalingModeProperty, BitmapScalingMode.HighQuality);
            borderImgFactory.AppendChild(imgFactory);
            spFactory.AppendChild(borderImgFactory);

            var tbFactory = new FrameworkElementFactory(typeof(TextBlock));
            tbFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Produkt"));
            tbFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            tbFactory.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            tbFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            spFactory.AppendChild(tbFactory);

            var produktTemplate = new DataTemplate { VisualTree = spFactory };

            dg.Columns.Add(new DataGridTemplateColumn
            {
                Header = "Produkt",
                CellTemplate = produktTemplate,
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 90
            });
            dg.Columns.Add(new DataGridTextColumn { Header = "Ilość",                          Binding = new System.Windows.Data.Binding("Ilosc")   { StringFormat = "N2" },     Width = new DataGridLength(1, DataGridLengthUnitType.Auto), MinWidth = 95,  ElementStyle = rightStyle });
            dg.Columns.Add(new DataGridTextColumn { Header = $"Cena ({walutaSymbol})",         Binding = new System.Windows.Data.Binding("Cena"),                                  Width = new DataGridLength(1, DataGridLengthUnitType.Auto), MinWidth = 85,  ElementStyle = rightStyle });
            dg.Columns.Add(new DataGridTextColumn { Header = $"Wartość ({walutaSymbol})",      Binding = new System.Windows.Data.Binding("Wartosc") { StringFormat = "N2" },     Width = new DataGridLength(1, DataGridLengthUnitType.Auto), MinWidth = 110, ElementStyle = rightStyle });
        }

        // ════════════════════════════════════════════════════════════
        // ZMIANY — DIFF (parsuje OpisZmiany na "Pole | Było | Jest | Δ")
        // ════════════════════════════════════════════════════════════
        private async Task LoadChangesAsync(int orderId, CancellationToken ct)
        {
            var cards = new List<DiffCardModel>();
            var historyRows = new List<HistoryRow>();
            string[] polskieMiesiace = { "", "sty", "lut", "mar", "kwi", "maj", "cze", "lip", "sie", "wrz", "paź", "lis", "gru" };

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync(ct);

                const string sql = @"SELECT TOP 30 OpisZmiany, UzytkownikNazwa, DataZmiany
                                     FROM [dbo].[HistoriaZmianZamowien]
                                     WHERE ZamowienieId = @OrderId AND TypZmiany = 'EDYCJA'
                                     ORDER BY DataZmiany DESC";
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@OrderId", orderId);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    string opis = reader.IsDBNull(0) ? "" : reader.GetString(0);
                    string kto  = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    DateTime? kiedy = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
                    string kiedyStr = kiedy.HasValue
                        ? $"{polskieMiesiace[kiedy.Value.Month]} {kiedy.Value.Day} {kiedy.Value:HH:mm}"
                        : "";

                    historyRows.Add(new HistoryRow
                    {
                        Opis = string.IsNullOrEmpty(opis) ? "(brak opisu)" : opis,
                        Meta = string.IsNullOrEmpty(kto) ? kiedyStr : $"{kto} • {kiedyStr}"
                    });

                    // Parse OpisZmiany do listy kart, każda z meta info kto/kiedy
                    foreach (var row in ParseDiff(opis))
                    {
                        cards.Add(new DiffCardModel
                        {
                            Pole  = row.Pole,
                            Bylo  = row.Bylo,
                            Jest  = row.Jest,
                            Delta = row.Delta,
                            Icon  = ChooseIcon(row.Pole),
                            Kto   = kto,
                            Kiedy = kiedyStr,
                            DeltaSign = ComputeDeltaSign(row.Bylo, row.Jest),
                            TowarImage = TryFindTowarImage(row.Pole, row.Bylo, row.Jest)
                        });
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadChanges error: {ex.Message}"); }

            if (ct.IsCancellationRequested) return;

            txtChangesCount.Text = cards.Count.ToString();

            if (cards.Count > 0)
            {
                icChangesCards.ItemsSource = cards;
                panelChangesDiff.Visibility = Visibility.Visible;
                panelNoChanges.Visibility = Visibility.Collapsed;
                badgeChanges.Visibility = Visibility.Visible;
                tabZmiany.Tag = "highlight";   // pomarańczowe podświetlenie zakładki
            }
            else
            {
                ShowNoChanges();
            }
        }

        // Polska deklinacja liczebnika: 1 zmiana, 2-4 zmiany, 5+ zmian
        private static string PluralPl(int n, string one, string few, string many)
        {
            int mod10 = n % 10, mod100 = n % 100;
            if (n == 1) return one;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return few;
            return many;
        }

        // Próbuje dopasować nazwę pola lub jedną z wartości do nazwy towaru z cache.
        // Jeśli któraś z fraz zawiera pełną nazwę towaru → zwraca obraz.
        private static BitmapImage? TryFindTowarImage(string pole, string bylo, string jest)
        {
            if (_productImagesCache.Count == 0) return null;

            // Łączymy 3 teksty żeby dać szansę matchowi w dowolnym z nich
            var haystack = $"{pole} {bylo} {jest}".ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(haystack)) return null;

            // Szukaj nazwy towaru >= 3 znaki, która jest substring'iem haystack'a
            foreach (var kv in _productNameCache)
            {
                var name = (kv.Value ?? "").Trim().ToLowerInvariant();
                if (name.Length < 3) continue;
                if (haystack.Contains(name) && _productImagesCache.TryGetValue(kv.Key, out var img))
                    return img;
            }

            // Fallback — kod towaru (krótszy match wymaga: separator z obu stron)
            foreach (var kv in _productCodeCache)
            {
                var kod = (kv.Value ?? "").Trim().ToLowerInvariant();
                if (kod.Length < 3) continue;
                // Whole-word match
                int idx = haystack.IndexOf(kod, StringComparison.Ordinal);
                if (idx < 0) continue;
                bool leftOk  = idx == 0 || !char.IsLetterOrDigit(haystack[idx - 1]);
                bool rightOk = idx + kod.Length == haystack.Length || !char.IsLetterOrDigit(haystack[idx + kod.Length]);
                if (leftOk && rightOk && _productImagesCache.TryGetValue(kv.Key, out var img))
                    return img;
            }

            return null;
        }

        // Wybierz emoji/glyph na podstawie nazwy zmienionego pola
        private static string ChooseIcon(string pole)
        {
            var p = (pole ?? "").ToLowerInvariant();
            if (p.Contains("notatk") || p.Contains("notes") || p.Contains("uwag") || p.Contains("komentarz")) return "📝";
            if (p.Contains("data") || p.Contains("termin") || p.Contains("godz")  || p.Contains("kalendarz")) return "📅";
            if (p.Contains("ilość") || p.Contains("ilosc") || p.Contains("kg"))                                return "Σ";
            if (p.Contains("cena")  || p.Contains("zł")    || p.Contains("zl"))                                return "$";
            if (p.Contains("status"))                                                                          return "◉";
            if (p.Contains("transport") || p.Contains("kierowca") || p.Contains("pojazd"))                     return "🚛";
            if (p.Contains("klient") || p.Contains("odbior") || p.Contains("kontrahent"))                      return "◐";
            if (p.Contains("towar") || p.Contains("produkt"))                                                  return "▦";
            if (p.Contains("adres") || p.Contains("miast"))                                                    return "◎";
            return "✎"; // generic edit
        }

        // Znak różnicy do kolorowania badge'a
        private static int? ComputeDeltaSign(string bylo, string jest)
        {
            if (decimal.TryParse((bylo ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var b) &&
                decimal.TryParse((jest ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var j))
            {
                if (j > b) return  1;
                if (j < b) return -1;
                return 0;
            }
            return null;
        }


        // Wzorce wykrywania w opisie zmiany — od najbardziej specyficznego do najogólniejszego
        private static readonly Regex[] _diffPatterns = new[]
        {
            // "X: A → B"  /  "X: A -> B"
            new Regex(@"^(?<pole>[^:→\-=]+?)[:\s]+(?<bylo>.+?)\s*→\s*(?<jest>.+)$",                                            RegexOptions.Compiled),
            new Regex(@"^(?<pole>[^:→\-=]+?)[:\s]+(?<bylo>.+?)\s*->\s*(?<jest>.+)$",                                           RegexOptions.Compiled),
            // "X z A na B"
            new Regex(@"^(?<pole>[^:→\-=]+?)\s+z\s+(?<bylo>.+?)\s+na\s+(?<jest>.+)$",                                          RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // "Zmieniono X z A na B"
            new Regex(@"^(?:Zmieniono|Zaktualizowano|Edytowano)\s+(?<pole>.+?)\s+z\s+['""]?(?<bylo>.+?)['""]?\s+na\s+['""]?(?<jest>.+?)['""]?$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // "Zmieniono X: A → B"  /  "Zmieniono X: A -> B"
            new Regex(@"^(?:Zmieniono|Zaktualizowano|Edytowano)\s+(?<pole>[^:]+?)[:\s]+(?<bylo>.+?)\s*[→\-]>?\s*(?<jest>.+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // "Ustawiono X na B"  /  "Dodano X: B"  (brak starej wartości)
            new Regex(@"^(?:Ustawiono|Dodano|Wpisano)\s+(?<pole>.+?)\s+(?:na|=|:)\s+['""]?(?<jest>.+?)['""]?$",                RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // "Usunięto X" / "Wyczyszczono X" (brak nowej wartości)
            new Regex(@"^(?:Usunięto|Wyczyszczono|Skasowano)\s+(?<pole>.+)$",                                                  RegexOptions.Compiled | RegexOptions.IgnoreCase),
            // "Zmieniono X" (zostało zmodyfikowane, brak A→B w opisie — np. notatka)
            new Regex(@"^(?:Zmieniono|Zaktualizowano|Edytowano)\s+(?<pole>.+)$",                                               RegexOptions.Compiled | RegexOptions.IgnoreCase)
        };

        private static IEnumerable<DiffRow> ParseDiff(string opis)
        {
            if (string.IsNullOrWhiteSpace(opis)) yield break;

            // Może być wiele zmian w jednym opisie (oddzielone ; lub nową linią)
            foreach (var line in opis.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                DiffRow? matched = null;
                foreach (var rx in _diffPatterns)
                {
                    var m = rx.Match(trimmed);
                    if (!m.Success) continue;

                    var pole = m.Groups["pole"].Value.Trim().TrimEnd(':', '.', ',');
                    var bylo = m.Groups["bylo"].Success ? m.Groups["bylo"].Value.Trim().Trim('"', '\'') : "";
                    var jest = m.Groups["jest"].Success ? m.Groups["jest"].Value.Trim().Trim('"', '\'') : "";

                    matched = new DiffRow
                    {
                        Pole  = CapitalizeFirst(pole),
                        Bylo  = bylo,
                        Jest  = jest,
                        Delta = ComputeDelta(bylo, jest)
                    };
                    break;
                }

                // Fallback: gdy żaden wzorzec nie pasuje — pokazujemy surowy tekst jako "Pole" (nic nie ginie)
                yield return matched ?? new DiffRow { Pole = CapitalizeFirst(trimmed), Bylo = "", Jest = "", Delta = "" };
            }
        }

        private static string CapitalizeFirst(string s)
            => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        private static string ComputeDelta(string bylo, string jest)
        {
            if (decimal.TryParse(bylo.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var b) &&
                decimal.TryParse(jest.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var j))
            {
                var d = j - b;
                return d == 0 ? "—" : (d > 0 ? $"+{d:N2}" : $"{d:N2}");
            }
            return "";
        }

        private void DgOrders_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentOrderId.HasValue)
            {
                var widok = new Kalendarz1.WidokZamowienia(UserID, _currentOrderId.Value);
                widok.ShowDialog();
                _ = RefreshDataAsync();
            }
        }

        private void ClearDetails()
        {
            _currentOrderId = null;
            // paneOrderInfo zostaje widoczny — tylko teksty wracają do "pustego" stanu
            txtOdbiorca.Text = "— wybierz zamówienie z listy —";
            txtOrderMeta.Text = "";
            _dtDetails.Clear();
            dgDetails.ItemsSource = null;
            _dtInvoice.Clear();
            _invoiceCompareItems.Clear();
            dgInvoiceItems.ItemsSource = null;
            dgInvoiceItems.Visibility = Visibility.Collapsed;
            panelInvoiceEmpty.Visibility = Visibility.Visible;
            txtInvoiceHint.Text = "Zamówienie jeszcze nie zafakturowane";
            invoiceHeader.Visibility = Visibility.Collapsed;
            invoiceSummary.Visibility = Visibility.Collapsed;
            txtInvoiceMatchTag.Visibility = Visibility.Collapsed;
            _flowItems.Clear();
            dgFlowItems.Visibility = Visibility.Collapsed;
            flowSummary.Visibility = Visibility.Collapsed;
            panelFlowEmpty.Visibility = Visibility.Visible;
            txtFlowHint.Text = "Wybierz zamówienie z listy";
            btnMarkFakturowane.IsEnabled = false;
            btnCofnijFakturowanie.IsEnabled = false;
            borderTransport.Visibility = Visibility.Collapsed;
            ShowNoChanges();
            badgeChanges.Visibility = Visibility.Collapsed;
            _suppressNotatkiSave = true;
            txtNotatki.Text = "";
            _suppressNotatkiSave = false;
        }

        // ════════════════════════════════════════════════════════════
        // CONTEXT MENU - Dopasuj fakturę / wyczyść
        // ════════════════════════════════════════════════════════════
        private async void MenuItem_DopasujFakture_Click(object sender, RoutedEventArgs e)
        {
            if (dgOrders.SelectedItem is not ZamowienieViewModel vm) return;
            var info = vm.Info;

            if (info.KlientId <= 0)
            {
                MessageBox.Show(this, "Brak ID klienta dla tego zamówienia.", "Nie można dopasować",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new DopasujFaktureWindow(
                _connHandel,
                info.KlientId,
                info.Klient,
                info.Id,
                info.TotalIlosc,
                info.NumerFaktury)
            {
                Owner = this
            };

            if (dlg.ShowDialog() != true) return;
            if (string.IsNullOrWhiteSpace(dlg.SelectedNumerFaktury)) return;

            try
            {
                await UpdateZamowienieFakturaAsync(info.Id, true, dlg.SelectedNumerFaktury);
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Błąd zapisu: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void MenuItem_WyczyscFakture_Click(object sender, RoutedEventArgs e)
        {
            if (dgOrders.SelectedItem is not ZamowienieViewModel vm) return;
            var info = vm.Info;

            if (string.IsNullOrEmpty(info.NumerFaktury) && !info.CzyZafakturowane) return;

            var ok = MessageBox.Show(this,
                $"Wyczyścić numer faktury i oznaczyć zamówienie #{info.Id} jako niezafakturowane?",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (ok != MessageBoxResult.Yes) return;

            try
            {
                await UpdateZamowienieFakturaAsync(info.Id, false, null);
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Błąd zapisu: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════════════════════════════════════════════
        // AKCJE BIZNESOWE z UNDO
        // ════════════════════════════════════════════════════════════
        private async void BtnAcceptChange_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue) return;
            var orderId = _currentOrderId.Value;

            try
            {
                await UpdateZamowienieFlagAsync(orderId, "CzyZmodyfikowaneDlaFaktur", false);
                ShowUndoBanner("Zmiana przyjęta", async () =>
                {
                    await UpdateZamowienieFlagAsync(orderId, "CzyZmodyfikowaneDlaFaktur", true);
                });
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnMarkFakturowane_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue) return;
            var orderId = _currentOrderId.Value;

            try
            {
                await UpdateZamowienieFlagAsync(orderId, "CzyZafakturowane", true);
                ShowUndoBanner("Oznaczono jako zafakturowane", async () =>
                {
                    await UpdateZamowienieFakturaAsync(orderId, false, null);
                });
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnCofnijFakturowanie_Click(object sender, RoutedEventArgs e)
        {
            if (!_currentOrderId.HasValue) return;
            var orderId = _currentOrderId.Value;

            try
            {
                await UpdateZamowienieFakturaAsync(orderId, false, null);
                ShowUndoBanner("Cofnięto fakturowanie", async () =>
                {
                    await UpdateZamowienieFlagAsync(orderId, "CzyZafakturowane", true);
                });
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task UpdateZamowienieFlagAsync(int orderId, string columnName, bool value)
        {
            // columnName z whitelistą — chroni przed SQL injection
            string allowed = columnName switch
            {
                "CzyZafakturowane" => "CzyZafakturowane",
                "CzyZmodyfikowaneDlaFaktur" => "CzyZmodyfikowaneDlaFaktur",
                _ => throw new ArgumentException("Nieobsługiwana kolumna")
            };
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand($"UPDATE [dbo].[ZamowieniaMieso] SET [{allowed}] = @V WHERE Id = @Id", cn);
            cmd.Parameters.AddWithValue("@V", value);
            cmd.Parameters.AddWithValue("@Id", orderId);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task UpdateZamowienieFakturaAsync(int orderId, bool zafakturowane, string? numerFaktury)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(
                "UPDATE [dbo].[ZamowieniaMieso] SET CzyZafakturowane = @V, NumerFaktury = @N WHERE Id = @Id", cn);
            cmd.Parameters.AddWithValue("@V", zafakturowane);
            cmd.Parameters.AddWithValue("@N", (object?)numerFaktury ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Id", orderId);
            await cmd.ExecuteNonQueryAsync();
        }

        // ════════════════════════════════════════════════════════════
        // UNDO BANNER (5 sek)
        // ════════════════════════════════════════════════════════════
        private void ShowUndoBanner(string message, Func<Task> undoAction)
        {
            txtUndoMsg.Text = message;
            undoBanner.Visibility = Visibility.Visible;
            _undoAction = async () => { try { await undoAction(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Undo error: {ex.Message}"); } };

            _undoTimer?.Stop();
            _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _undoTimer.Tick += (s, e) =>
            {
                _undoTimer?.Stop();
                undoBanner.Visibility = Visibility.Collapsed;
                _undoAction = null;
            };
            _undoTimer.Start();
        }

        private async void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            _undoTimer?.Stop();
            undoBanner.Visibility = Visibility.Collapsed;
            var act = _undoAction;
            _undoAction = null;
            if (act != null)
            {
                await act();
                await RefreshDataAsync();
            }
        }

        // ════════════════════════════════════════════════════════════
        // KEYBOARD (skróty są w toolTipach, więc warto mieć)
        // ════════════════════════════════════════════════════════════
        private void PanelFakturWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled) return;
            if (e.OriginalSource is TextBox || e.OriginalSource is ComboBox) return;

            if (e.Key == Key.F5)        { _ = RefreshDataAsync(); e.Handled = true; }
            else if (e.Key == Key.Left  && Keyboard.Modifiers == ModifierKeys.None) { BtnPrevDay_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.Right && Keyboard.Modifiers == ModifierKeys.None) { BtnNextDay_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.Home  && Keyboard.Modifiers == ModifierKeys.None) { BtnToday_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.N     && Keyboard.Modifiers == ModifierKeys.Control) { BtnNoweZamowienie_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (_currentOrderId.HasValue)
            {
                if (e.Key == Key.F && btnMarkFakturowane.IsEnabled) { BtnMarkFakturowane_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.U && btnCofnijFakturowanie.IsEnabled) { BtnCofnijFakturowanie_Click(this, new RoutedEventArgs()); e.Handled = true; }
                else if (e.Key == Key.Space && tabZmiany.IsSelected && panelChangesDiff.Visibility == Visibility.Visible)
                { BtnAcceptChange_Click(this, new RoutedEventArgs()); e.Handled = true; }
            }
        }

        // ════════════════════════════════════════════════════════════
        // DDL (raz na uruchomienie aplikacji)
        // ════════════════════════════════════════════════════════════
        private async Task EnsureColumnsExistAsync(SqlConnection cn)
        {
            const string sql = @"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'CzyZafakturowane')
                    ALTER TABLE [dbo].[ZamowieniaMieso] ADD CzyZafakturowane BIT DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'NumerFaktury')
                    ALTER TABLE [dbo].[ZamowieniaMieso] ADD NumerFaktury NVARCHAR(50) NULL;
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'CzyZmodyfikowaneDlaFaktur')
                    ALTER TABLE [dbo].[ZamowieniaMieso] ADD CzyZmodyfikowaneDlaFaktur BIT DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'DataOstatniejModyfikacji')
                    ALTER TABLE [dbo].[ZamowieniaMieso] ADD DataOstatniejModyfikacji DATETIME NULL;
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'ModyfikowalPrzez')
                    ALTER TABLE [dbo].[ZamowieniaMieso] ADD ModyfikowalPrzez NVARCHAR(100) NULL;
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'TransportKursID')
                    ALTER TABLE [dbo].[ZamowieniaMieso] ADD TransportKursID BIGINT NULL;
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'CzyZmodyfikowaneDlaMagazynu')
                    ALTER TABLE [dbo].[ZamowieniaMieso] ADD CzyZmodyfikowaneDlaMagazynu BIT DEFAULT 0;
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ZamowieniaMieso' AND COLUMN_NAME = 'CzyZmodyfikowaneDlaProdukcji')
                    ALTER TABLE [dbo].[ZamowieniaMieso] ADD CzyZmodyfikowaneDlaProdukcji BIT DEFAULT 0;";
            await using var cmd = new SqlCommand(sql, cn);
            await cmd.ExecuteNonQueryAsync();
        }

        // ════════════════════════════════════════════════════════════
        // DATA CLASSES
        // ════════════════════════════════════════════════════════════
        public class ZamowienieInfo
        {
            public int Id { get; set; }
            public int KlientId { get; set; }
            public string Klient { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public decimal TotalIlosc { get; set; }
            public decimal Wartosc { get; set; }
            public DateTime? DataZamowienia { get; set; }
            public DateTime? DataUboju { get; set; }
            public string Status { get; set; } = "";
            public string UtworzonePrzez { get; set; } = "";
            public bool CzyZafakturowane { get; set; }
            public string NumerFaktury { get; set; } = "";
            public long? TransportKursID { get; set; }
            public string GodzWyjazdu { get; set; } = "";
            public string Kierowca { get; set; } = "";
            public string Pojazd { get; set; } = "";
            public bool CzyZmodyfikowaneDlaFaktur { get; set; }
            public DateTime? DataOstatniejModyfikacji { get; set; }
            public string ModyfikowalPrzez { get; set; } = "";
            public bool CzyMaCeny { get; set; }
            public string Waluta { get; set; } = "PLN";

            // Wyniki weryfikacji faktura ↔ zamówienie (uzupełniane po VerifyInvoicesAsync)
            public InvoiceMatchStatus MatchStatus { get; set; } = InvoiceMatchStatus.None;
            public int? InvoiceKhId { get; set; }
            public string InvoiceKhName { get; set; } = "";
            public decimal? InvoiceTotalIlosc { get; set; }
        }

        public enum InvoiceMatchStatus
        {
            None,           // brak NumerFaktury - nic nie pokazuj
            NotFound,       // NumerFaktury jest, ale brak takiej w Symfonii (alarm)
            ClientMismatch, // klient zamówienia != klient faktury (alarm)
            QtyMismatch,    // klient OK, ilość się nie zgadza (ostrzeżenie)
            Ok              // wszystko gra
        }

        public enum PositionCompareStatus
        {
            Match,          // zamówienie i faktura zgodne (ilość ±tolerancja)
            QtyDiff,        // ten sam towar, różna ilość
            OnlyOnInvoice,  // pozycja jest tylko na fakturze (dodatkowa)
            OnlyOnOrder     // pozycja jest tylko na zamówieniu (brakuje na fakturze)
        }

        // Pojedynczy wiersz porównania zamówienie ↔ faktura w zakładce "Pozycje faktury"
        public class InvoicePositionCompare
        {
            private static readonly CultureInfo PL = CultureInfo.GetCultureInfo("pl-PL");
            private static readonly Brush BrushMatch     = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)));
            private static readonly Brush BrushDiff      = Freeze(new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)));
            private static readonly Brush BrushMissing   = Freeze(new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)));
            private static readonly Brush BrushExtra     = Freeze(new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E)));
            private static readonly Brush BrushTextDark  = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)));
            private static readonly Brush BrushTextMuted = Freeze(new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)));
            private static readonly Brush BgMatch        = Brushes.Transparent;
            private static readonly Brush BgDiff         = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xED)));
            private static readonly Brush BgMissing      = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2)));
            private static readonly Brush BgExtra        = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)));
            private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

            public BitmapImage? Image { get; set; }
            public string Produkt { get; set; } = "";
            public decimal? OrderQty { get; set; }
            public decimal? InvoiceQty { get; set; }
            public decimal? Cena { get; set; }
            public decimal? Wartosc { get; set; }
            public PositionCompareStatus Status { get; set; }

            public string OrderQtyStr   => OrderQty.HasValue   ? OrderQty.Value.ToString("N2", PL)   : "—";
            public string InvoiceQtyStr => InvoiceQty.HasValue ? InvoiceQty.Value.ToString("N2", PL) : "—";
            public string CenaStr       => Cena.HasValue       ? Cena.Value.ToString("N2", PL)      : "";
            public string WartoscStr    => Wartosc.HasValue    ? Wartosc.Value.ToString("N2", PL)   : "";

            public decimal DiffNum => (InvoiceQty ?? 0m) - (OrderQty ?? 0m);
            public string DiffStr
            {
                get
                {
                    if (Status == PositionCompareStatus.OnlyOnOrder)   return $"−{(OrderQty ?? 0m).ToString("N2", PL)}";
                    if (Status == PositionCompareStatus.OnlyOnInvoice) return $"+{(InvoiceQty ?? 0m).ToString("N2", PL)}";
                    if (Math.Abs(DiffNum) < 0.005m) return "0,00";
                    return (DiffNum > 0 ? "+" : "") + DiffNum.ToString("N2", PL);
                }
            }

            public string StatusIcon => Status switch
            {
                PositionCompareStatus.Match         => "✓",
                PositionCompareStatus.QtyDiff       => "⚠",
                PositionCompareStatus.OnlyOnInvoice => "+",
                PositionCompareStatus.OnlyOnOrder   => "✖",
                _ => ""
            };
            public string StatusText => Status switch
            {
                PositionCompareStatus.Match         => "Zgodne",
                PositionCompareStatus.QtyDiff       => "Różnica ilości",
                PositionCompareStatus.OnlyOnInvoice => "Tylko na fakturze",
                PositionCompareStatus.OnlyOnOrder   => "Brak na fakturze",
                _ => ""
            };
            public Brush DiffBrush => Status switch
            {
                PositionCompareStatus.Match         => BrushMatch,
                PositionCompareStatus.QtyDiff       => BrushDiff,
                PositionCompareStatus.OnlyOnInvoice => BrushExtra,
                PositionCompareStatus.OnlyOnOrder   => BrushMissing,
                _ => BrushTextDark
            };
            public Brush RowBg => Status switch
            {
                PositionCompareStatus.QtyDiff       => BgDiff,
                PositionCompareStatus.OnlyOnInvoice => BgExtra,
                PositionCompareStatus.OnlyOnOrder   => BgMissing,
                _ => BgMatch
            };
            public Brush OrderQtyBrush   => OrderQty.HasValue   ? BrushTextDark : BrushTextMuted;
            public Brush InvoiceQtyBrush => InvoiceQty.HasValue ? BrushTextDark : BrushTextMuted;
        }

        // Wiersz zakładki "Zam / Wyd / Fak" — przepływ produktu w kg.
        // Każda z trzech ilości może być null:
        //   Zamowiono = null  → towar nie był na zamówieniu (pojawił się dopiero na fakturze)
        //   Wydano    = null  → zamówienie jeszcze niewydane
        //   Zafakturowano = null → brak faktury lub towar pominięty
        public class ProductFlowItem
        {
            private static readonly CultureInfo PL = CultureInfo.GetCultureInfo("pl-PL");
            private static readonly Brush _txtDark = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)));
            private static readonly Brush _txtMuted = Freeze(new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)));
            private static readonly Brush _txtMissing = Freeze(new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))); // czerwony — brak / mniej
            private static readonly Brush _txtExtra = Freeze(new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)));   // pomarańczowy — nadwyżka
            private static readonly Brush _txtMatch = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)));   // zielony — zgodne
            private static readonly Brush _bgDiff = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xF7, 0xED)));     // delikatny pomarańcz
            private static readonly Brush _bgMissing = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2))); // delikatny czerwony
            private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

            public BitmapImage? Image { get; set; }
            public string Produkt { get; set; } = "";
            public decimal? Zamowiono { get; set; }
            public decimal? Wydano { get; set; }
            public decimal? Zafakturowano { get; set; }

            public string ZamowionoStr     => Zamowiono.HasValue     ? Zamowiono.Value.ToString("N2", PL)     : "—";
            public string WydanoStr        => Wydano.HasValue        ? Wydano.Value.ToString("N2", PL)        : "—";
            public string ZafakturowanoStr => Zafakturowano.HasValue ? Zafakturowano.Value.ToString("N2", PL) : "—";

            public Brush WydanoBrush
            {
                get
                {
                    if (!Wydano.HasValue) return _txtMuted;
                    if (!Zamowiono.HasValue) return _txtExtra;
                    decimal d = Wydano.Value - Zamowiono.Value;
                    if (Math.Abs(d) < 0.005m) return _txtMatch;
                    return d < 0 ? _txtMissing : _txtExtra;
                }
            }

            public Brush ZafakturowanoBrush
            {
                get
                {
                    if (!Zafakturowano.HasValue) return _txtMuted;
                    decimal? baseline = Wydano ?? Zamowiono;
                    if (!baseline.HasValue) return _txtExtra;
                    decimal d = Zafakturowano.Value - baseline.Value;
                    if (Math.Abs(d) < 0.005m) return _txtMatch;
                    return d < 0 ? _txtMissing : _txtExtra;
                }
            }

            public decimal? DeltaWydZam =>
                (Wydano.HasValue || Zamowiono.HasValue) ? (Wydano ?? 0m) - (Zamowiono ?? 0m) : (decimal?)null;

            public decimal? DeltaFakWyd
            {
                get
                {
                    if (!Zafakturowano.HasValue && !Wydano.HasValue) return null;
                    decimal baseline = Wydano ?? Zamowiono ?? 0m;
                    return (Zafakturowano ?? 0m) - baseline;
                }
            }

            public string DeltaWydZamStr => FormatDelta(DeltaWydZam);
            public string DeltaFakWydStr => FormatDelta(DeltaFakWyd);

            public Brush DeltaWydZamBrush => DeltaToBrush(DeltaWydZam);
            public Brush DeltaFakWydBrush => DeltaToBrush(DeltaFakWyd);

            // Pozycja ma jakąkolwiek niezerową różnicę → wyróżniamy
            public bool HasAnyDiff
            {
                get
                {
                    bool dwzNon0 = DeltaWydZam.HasValue && Math.Abs(DeltaWydZam.Value) >= 0.005m;
                    bool dfwNon0 = DeltaFakWyd.HasValue && Math.Abs(DeltaFakWyd.Value) >= 0.005m;
                    return dwzNon0 || dfwNon0;
                }
            }

            public Brush RowBg
            {
                get
                {
                    bool dwzMissing = DeltaWydZam.HasValue && DeltaWydZam.Value < -0.005m;
                    bool dfwMissing = DeltaFakWyd.HasValue && DeltaFakWyd.Value < -0.005m;
                    if (dwzMissing || dfwMissing) return _bgMissing;
                    if (HasAnyDiff) return _bgDiff;
                    return Brushes.Transparent;
                }
            }

            private static string FormatDelta(decimal? d)
            {
                if (!d.HasValue) return "";
                if (Math.Abs(d.Value) < 0.005m) return "0,00";
                return (d.Value > 0 ? "+" : "") + d.Value.ToString("N2", PL);
            }

            private static Brush DeltaToBrush(decimal? d)
            {
                if (!d.HasValue) return _txtMuted;
                if (Math.Abs(d.Value) < 0.005m) return _txtMatch;
                return d.Value < 0 ? _txtMissing : _txtExtra;
            }
        }

        public class DiffRow
        {
            public string Pole { get; set; } = "";
            public string Bylo { get; set; } = "";
            public string Jest { get; set; } = "";
            public string Delta { get; set; } = "";
        }

        // Karta rozwijalna w zakładce Zmiany
        public class DiffCardModel : INotifyPropertyChanged
        {
            private static readonly Brush _green     = Freeze(new SolidColorBrush(Color.FromRgb(0x06, 0x5F, 0x46)));
            private static readonly Brush _red       = Freeze(new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B)));
            private static readonly Brush _gray      = Freeze(new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)));
            private static readonly Brush _greenSoft = Freeze(new SolidColorBrush(Color.FromRgb(0xEC, 0xFD, 0xF5)));
            private static readonly Brush _redSoft   = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xF2, 0xF2)));
            private static readonly Brush _graySoft  = Freeze(new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)));
            private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

            public string Pole  { get; set; } = "";
            public string Bylo  { get; set; } = "";
            public string Jest  { get; set; } = "";
            public string Delta { get; set; } = "";
            public string Icon  { get; set; } = "•";
            public string Kto   { get; set; } = "";
            public string Kiedi { get => Kiedy; set => Kiedy = value; }
            public string Kiedy { get; set; } = "";
            public BitmapImage? TowarImage { get; set; }    // wypełnione gdy zmiana dotyczy konkretnego towaru
            public bool HasImage => TowarImage != null;
            public Visibility ImageVis => HasImage ? Visibility.Visible : Visibility.Collapsed;
            public Visibility IconVis  => HasImage ? Visibility.Collapsed : Visibility.Visible;

            // Compact summary widoczny gdy karta zwinięta
            public string ShortSummary
            {
                get
                {
                    if (string.IsNullOrEmpty(Bylo) && string.IsNullOrEmpty(Jest)) return "";
                    return $"{Bylo}  →  {Jest}";
                }
            }

            // 0 = positive (green), <0 = negative (red), null = neutral
            public int? DeltaSign { get; set; }

            public Brush DeltaColor => DeltaSign switch
            {
                > 0 => _green,
                < 0 => _red,
                _   => _gray
            };
            public Brush DeltaBgColor => DeltaSign switch
            {
                > 0 => _greenSoft,
                < 0 => _redSoft,
                _   => _graySoft
            };

            public bool HasDelta => !string.IsNullOrEmpty(Delta);
            public bool HasMeta  => !string.IsNullOrEmpty(Kto) || !string.IsNullOrEmpty(Kiedy);

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        public class HistoryRow
        {
            public string Opis { get; set; } = "";
            public string Meta { get; set; } = "";
        }

        // ────────────────────────────────────────────────────────────
        // ViewModel — wszystkie Brushe statyczne (frozen) i cache'owane
        // ────────────────────────────────────────────────────────────
        public class ZamowienieViewModel : INotifyPropertyChanged
        {
            // Cache statycznych Brushy — frozen, bezpieczne dla UI thread
            private static readonly Brush _brushWarning   = Freeze(new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)));
            private static readonly Brush _brushSuccess   = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)));
            private static readonly Brush _brushDanger    = Freeze(new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)));
            private static readonly Brush _brushPrimary   = Freeze(new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)));
            private static readonly Brush _brushTransp    = Brushes.Transparent;
            private static readonly Brush _brushTextDark  = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)));
            private static readonly Brush _brushTextMuted = Freeze(new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)));

            // Status badge brushes
            private static readonly Brush _bgZafakt   = Freeze(new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)));
            private static readonly Brush _txtZafakt  = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0x65, 0x34)));
            private static readonly Brush _bgZmiana   = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xD7, 0xAA)));
            private static readonly Brush _txtZmiana  = Freeze(new SolidColorBrush(Color.FromRgb(0xC2, 0x41, 0x0C)));
            private static readonly Brush _bgWydano   = Freeze(new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE)));
            private static readonly Brush _txtWydano  = Freeze(new SolidColorBrush(Color.FromRgb(0x1E, 0x40, 0xAF)));
            private static readonly Brush _bgZreal    = Freeze(new SolidColorBrush(Color.FromRgb(0xDC, 0xFC, 0xE7)));
            private static readonly Brush _txtZreal   = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0x65, 0x34)));
            private static readonly Brush _bgNowe     = Freeze(new SolidColorBrush(Color.FromRgb(0xFE, 0xF3, 0xC7)));
            private static readonly Brush _txtNowe    = Freeze(new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E)));
            private static readonly Brush _bgDefault  = Freeze(new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF1)));
            private static readonly Brush _txtDefault = Freeze(new SolidColorBrush(Color.FromRgb(0x54, 0x6E, 0x7A)));

            // Row backgrounds — clean: białe wszystko, status pokazuje pasek + badge
            // Tylko zafakturowane mają bardzo subtelne wyszarzenie (już zrobione, nieaktywne wizualnie)
            private static readonly Brush _rowZafakt  = Freeze(new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)));
            private static readonly Brush _rowZmiana  = Brushes.White;
            private static readonly Brush _rowDefault = Brushes.White;

            // Strip colors (lewa pionowa kreska 4px)
            private static readonly Brush _stripWarn = Freeze(new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)));
            private static readonly Brush _stripDang = Freeze(new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)));
            private static readonly Brush _stripOk   = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)));
            private static readonly Brush _stripBlue = Freeze(new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)));
            private static readonly Brush _stripGray = Freeze(new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)));

            private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

            public ZamowienieInfo Info { get; }
            public ZamowienieViewModel(ZamowienieInfo info) { Info = info; }

            public string KlientName => Info.Klient;
            public bool   HasChange  => Info.CzyZmodyfikowaneDlaFaktur;
            public string Handlowiec => Info.Handlowiec;
            public BitmapSource? HandlowiecAvatar => string.IsNullOrEmpty(Info.Handlowiec)
                ? null
                : (_handlowiecAvatarCache.TryGetValue(Info.Handlowiec, out var av) ? av : null);
            public Visibility HasAvatarVis => HandlowiecAvatar != null ? Visibility.Visible : Visibility.Collapsed;
            public decimal TotalIlosc => Info.TotalIlosc;
            public decimal Wartosc    => Info.Wartosc;
            public bool HasTransport => !string.IsNullOrEmpty(Info.GodzWyjazdu);
            public string GodzWyjazdu => Info.GodzWyjazdu ?? "";
            public string Kierowca    => Info.Kierowca ?? "";
            public string NumerFaktury => Info.NumerFaktury ?? "";

            // ─── Status dopasowania faktury (ikona + kolor + tooltip obok klienta) ───
            public string MatchIcon => Info.MatchStatus switch
            {
                InvoiceMatchStatus.NotFound       => "⚠",
                InvoiceMatchStatus.ClientMismatch => "✖",
                InvoiceMatchStatus.QtyMismatch    => "⚠",
                InvoiceMatchStatus.Ok             => "✓",
                _ => ""
            };
            private static readonly Brush _matchOk     = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)));
            private static readonly Brush _matchWarn   = Freeze(new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C)));
            private static readonly Brush _matchAlarm  = Freeze(new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26)));
            public Brush MatchBrush => Info.MatchStatus switch
            {
                InvoiceMatchStatus.NotFound       => _matchAlarm,
                InvoiceMatchStatus.ClientMismatch => _matchAlarm,
                InvoiceMatchStatus.QtyMismatch    => _matchWarn,
                InvoiceMatchStatus.Ok             => _matchOk,
                _ => Brushes.Transparent
            };
            public string MatchTooltip => Info.MatchStatus switch
            {
                InvoiceMatchStatus.NotFound       => $"Faktura {Info.NumerFaktury} nie istnieje w Symfonii",
                InvoiceMatchStatus.ClientMismatch => $"✖ Klient na fakturze: {(string.IsNullOrEmpty(Info.InvoiceKhName) ? $"khid={Info.InvoiceKhId}" : Info.InvoiceKhName)}\nKlient zamówienia: {Info.Klient}",
                InvoiceMatchStatus.QtyMismatch    => $"⚠ Suma kg na fakturze ({Info.InvoiceTotalIlosc:N0}) ≠ na zamówieniu ({Info.TotalIlosc:N0})\nKlient na fakturze: {Info.InvoiceKhName}",
                InvoiceMatchStatus.Ok             => $"✓ Faktura {Info.NumerFaktury}\nKlient: {Info.InvoiceKhName}\nSuma kg: {Info.InvoiceTotalIlosc:N0}",
                _ => ""
            };
            public Visibility MatchVis => Info.MatchStatus == InvoiceMatchStatus.None
                ? Visibility.Collapsed : Visibility.Visible;

            public string CenaDisplay => Info.CzyMaCeny ? "✓" : "✗";
            public Brush  CenaColor   => Info.CzyMaCeny ? _brushSuccess : _brushDanger;

            // Waluta — symbol dla wyświetlania
            public string Waluta => Info.Waluta ?? "PLN";
            public string WalutaSymbol => Waluta == "EUR" ? "€" : "zł";

            // Wartość sformatowana z walutą — np. "1 500,00 zł" lub "350,00 €"
            public string WartoscDisplay => $"{Info.Wartosc:N2} {WalutaSymbol}";

            // Wartość — szara gdy 0 (brak cen)
            public Brush WartoscColor => Info.Wartosc > 0 ? _brushTextDark : _brushTextMuted;

            // Status display + kolory
            public string StatusDisplay
            {
                get
                {
                    // Najpierw kombinacja: zafakturowane + zmiana = wyższy priorytet, własna grupa
                    if (Info.CzyZafakturowane && Info.CzyZmodyfikowaneDlaFaktur)
                        return "⚠ Zafakturowane — zmiana po fakturze";
                    if (Info.CzyZafakturowane) return "Zafakturowane";
                    if (Info.CzyZmodyfikowaneDlaFaktur) return "⚠ Do zatwierdzenia";
                    return string.IsNullOrEmpty(Info.Status) ? "—" : Info.Status;
                }
            }

            public Brush StatusColor
            {
                get
                {
                    if (Info.CzyZmodyfikowaneDlaFaktur) return _txtZmiana;
                    if (Info.CzyZafakturowane) return _txtZafakt;
                    return Info.Status switch
                    {
                        "Wydano"       => _txtWydano,
                        "Zrealizowane" => _txtZreal,
                        "W realizacji" => _txtWydano,
                        "Nowe"         => _txtNowe,
                        _              => _txtDefault
                    };
                }
            }

            public Brush StatusBackground
            {
                get
                {
                    if (Info.CzyZmodyfikowaneDlaFaktur) return _bgZmiana;
                    if (Info.CzyZafakturowane) return _bgZafakt;
                    return Info.Status switch
                    {
                        "Wydano"       => _bgWydano,
                        "Zrealizowane" => _bgZreal,
                        "W realizacji" => _bgWydano,
                        "Nowe"         => _bgNowe,
                        _              => _bgDefault
                    };
                }
            }

            // Tło wiersza — subtelne, nie ostre
            public Brush RowBackground
            {
                get
                {
                    if (Info.CzyZmodyfikowaneDlaFaktur) return _rowZmiana;
                    if (Info.CzyZafakturowane) return _rowZafakt;
                    return _rowDefault;
                }
            }

            // 4px lewy pasek priorytetu — najważniejszy wskaźnik bo widoczny zawsze
            public Brush PriorityStripColor
            {
                get
                {
                    if (Info.CzyZmodyfikowaneDlaFaktur) return _stripWarn;   // pomarańczowy = wymaga uwagi
                    if (!Info.CzyMaCeny) return _stripDang;                  // czerwony = brak cen
                    if (Info.CzyZafakturowane) return _stripOk;              // zielony = załatwione
                    if (Info.Status == "Nowe") return _stripBlue;            // niebieski = nowe
                    return _stripGray;                                       // szary = neutralne
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;
        }

        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    }
}
