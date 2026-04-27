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
        private bool _showFakturowane = false;
        private int? _selectedProductId = null;
        private bool _isRefreshing = false;
        private bool _pendingRefresh = false;

        private readonly DataTable _dtDetails = new();

        // Cache (static — przeżywa zamknięcia okna)
        private static readonly Dictionary<int, (string Name, string Salesman)> _contractorsCache = new();
        private static readonly Dictionary<int, string> _productCodeCache = new();   // Id → kod
        private static readonly Dictionary<int, string> _productNameCache = new();   // Id → nazwa (pełna)
        private static bool _columnsEnsured = false;
        private static DateTime _contractorsCacheLoadedAt = DateTime.MinValue;

        // CancellationToken dla SelectionChanged (likwiduje race condition)
        private CancellationTokenSource? _detailsCts;

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
                LoadProductsCacheAsync()
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
                txtStatusInfo.Text = "Ładowanie...";
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
                             zm.CzyZmodyfikowaneDlaFaktur, zm.DataOstatniejModyfikacji, zm.ModyfikowalPrzez
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
                    bool czyMaCeny = !reader.IsDBNull(14) && Convert.ToInt32(reader.GetValue(14)) == 1;

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
                        ModyfikowalPrzez = modyfikowalPrzez, CzyMaCeny = czyMaCeny
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

                // BATCH update — zamiast Clear() + 50× Add() (= 51 powiadomień CollectionChanged)
                var newList = new ObservableCollection<ZamowienieViewModel>();
                foreach (var info in tempList)
                {
                    if (!_showFakturowane && info.CzyZafakturowane) continue;
                    newList.Add(new ZamowienieViewModel(info));
                }
                ZamowieniaList = newList;
                OnPropertyChanged(nameof(ZamowieniaList));
                dgOrders.ItemsSource = ZamowieniaList;

                UpdateContextCounters(tempList);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania zamówień: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatusInfo.Text = "Błąd ładowania";
            }
        }

        private void UpdateContextCounters(List<ZamowienieInfo> all)
        {
            int doFaktury = all.Count(z => !z.CzyZafakturowane && !z.CzyZmodyfikowaneDlaFaktur);
            int zafakt    = all.Count(z =>  z.CzyZafakturowane);
            int zmian     = all.Count(z =>  z.CzyZmodyfikowaneDlaFaktur);
            decimal kwotaDoFaktury = all.Where(z => !z.CzyZafakturowane).Sum(z => z.Wartosc);

            txtCntDoFaktury.Text = $"{doFaktury} do faktury";
            txtCntZafakt.Text    = $"{zafakt} zafakt.";
            txtCntZmian.Text     = $"{zmian} ⚠ zmian";
            txtCntKwota.Text     = $"{kwotaDoFaktury:N0} zł";

            txtStatusInfo.Text =
                $"{_selectedDate:dd.MM.yyyy} (dz.{(int)_selectedDate.DayOfWeek}) • " +
                $"łącznie {all.Count} zam. • do faktury: {kwotaDoFaktury:N0} zł";
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
                await LoadSaldoKlientaAsync(vm.Info.KlientId, ct);
            }
            catch (OperationCanceledException) { }
        }

        private void ShowNoChanges()
        {
            panelNoChanges.Visibility = Visibility.Visible;
            panelChangesDiff.Visibility = Visibility.Collapsed;
            icHistoryList.ItemsSource = null;
        }

        private async Task LoadOrderDetailsAsync(ZamowienieViewModel vm, CancellationToken ct)
        {
            _dtDetails.Clear();
            _dtDetails.Columns.Clear();
            _dtDetails.Columns.Add("Produkt", typeof(string));
            _dtDetails.Columns.Add("Ilosc", typeof(decimal));
            _dtDetails.Columns.Add("Cena", typeof(string));
            _dtDetails.Columns.Add("Wartosc", typeof(decimal));

            txtOdbiorca.Text = vm.Info.Klient;
            txtHandlowiec.Text = $"Handlowiec: {(string.IsNullOrEmpty(vm.Info.Handlowiec) ? "brak" : vm.Info.Handlowiec)}";
            txtDataZamowienia.Text = vm.Info.DataZamowienia.HasValue
                ? $"Zamówienie: {vm.Info.DataZamowienia.Value:dd.MM.yyyy}"
                : "";

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

                    string nazwa = _productNameCache.TryGetValue(kod, out var n) ? n
                                 : _productCodeCache.TryGetValue(kod, out var c) ? c
                                 : $"Towar {kod}";

                    var row = _dtDetails.NewRow();
                    row["Produkt"] = nazwa;
                    row["Ilosc"]   = ilosc;
                    row["Cena"]    = cena;
                    row["Wartosc"] = ilosc * cenaNum;
                    _dtDetails.Rows.Add(row);
                }

                if (ct.IsCancellationRequested) return;
                SetupDetailsGrid();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    MessageBox.Show($"Błąd ładowania pozycji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupDetailsGrid()
        {
            dgDetails.ItemsSource = _dtDetails.DefaultView;
            dgDetails.Columns.Clear();

            var rightStyle = new Style(typeof(TextBlock));
            rightStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Right));

            dgDetails.Columns.Add(new DataGridTextColumn { Header = "Produkt", Binding = new System.Windows.Data.Binding("Produkt"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            dgDetails.Columns.Add(new DataGridTextColumn { Header = "Ilość", Binding = new System.Windows.Data.Binding("Ilosc")   { StringFormat = "N2" }, Width = new DataGridLength(1, DataGridLengthUnitType.Auto), MinWidth = 70, ElementStyle = rightStyle });
            dgDetails.Columns.Add(new DataGridTextColumn { Header = "Cena",  Binding = new System.Windows.Data.Binding("Cena"),                            Width = new DataGridLength(1, DataGridLengthUnitType.Auto), MinWidth = 60, ElementStyle = rightStyle });
            dgDetails.Columns.Add(new DataGridTextColumn { Header = "Wartość", Binding = new System.Windows.Data.Binding("Wartosc") { StringFormat = "N2" }, Width = new DataGridLength(1, DataGridLengthUnitType.Auto), MinWidth = 80, ElementStyle = rightStyle });
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
                            DeltaSign = ComputeDeltaSign(row.Bylo, row.Jest)
                        });
                    }
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LoadChanges error: {ex.Message}"); }

            if (ct.IsCancellationRequested) return;

            txtZmianaNaglowek.Text = $"Wykryto {historyRows.Count} {PluralPl(historyRows.Count, "zmianę", "zmiany", "zmian")} w zamówieniu";
            txtChangesCount.Text   = $"{cards.Count} {PluralPl(cards.Count, "zmiana", "zmiany", "zmian")}";

            if (cards.Count > 0)
            {
                icChangesCards.ItemsSource = cards;
                panelChangesDiff.Visibility = Visibility.Visible;
                panelNoChanges.Visibility = Visibility.Collapsed;
            }
            else
            {
                ShowNoChanges();
            }
            icHistoryList.ItemsSource = historyRows;
        }

        // Polska deklinacja liczebnika: 1 zmiana, 2-4 zmiany, 5+ zmian
        private static string PluralPl(int n, string one, string few, string many)
        {
            int mod10 = n % 10, mod100 = n % 100;
            if (n == 1) return one;
            if (mod10 >= 2 && mod10 <= 4 && (mod100 < 10 || mod100 >= 20)) return few;
            return many;
        }

        // Wybierz unicode glyph na podstawie nazwy zmienionego pola
        private static string ChooseIcon(string pole)
        {
            var p = (pole ?? "").ToLowerInvariant();
            if (p.Contains("ilość") || p.Contains("ilosc") || p.Contains("kg")) return "Σ";
            if (p.Contains("cena") || p.Contains("zł") || p.Contains("zl"))    return "$";
            if (p.Contains("data") || p.Contains("termin") || p.Contains("godz")) return "◷";
            if (p.Contains("status"))                                            return "◉";
            if (p.Contains("transport") || p.Contains("kierowca"))               return "▣";
            if (p.Contains("klient") || p.Contains("odbior"))                    return "◐";
            if (p.Contains("towar") || p.Contains("produkt"))                    return "▦";
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


        // Próbuje wykryć w tekście wzorzec "pole: było → jest" w różnych wariantach
        private static readonly Regex[] _diffPatterns = new[]
        {
            new Regex(@"^(?<pole>[^:→\-=]+?)[:\s]+(?<bylo>[^→\-]+?)\s*[→]\s*(?<jest>.+)$",      RegexOptions.Compiled),
            new Regex(@"^(?<pole>[^:→\-=]+?)[:\s]+(?<bylo>[^→\-]+?)\s*->\s*(?<jest>.+)$",        RegexOptions.Compiled),
            new Regex(@"^(?<pole>[^:→\-=]+?)\s*z\s+(?<bylo>[^→\-]+?)\s+na\s+(?<jest>.+)$",       RegexOptions.Compiled | RegexOptions.IgnoreCase)
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
                    if (m.Success)
                    {
                        var bylo = m.Groups["bylo"].Value.Trim();
                        var jest = m.Groups["jest"].Value.Trim();
                        matched = new DiffRow
                        {
                            Pole = m.Groups["pole"].Value.Trim(),
                            Bylo = bylo,
                            Jest = jest,
                            Delta = ComputeDelta(bylo, jest)
                        };
                        break;
                    }
                }

                yield return matched ?? new DiffRow { Pole = trimmed, Bylo = "", Jest = "", Delta = "" };
            }
        }

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

        // ════════════════════════════════════════════════════════════
        // SALDO KLIENTA (placeholder — ToDo: pełne dane z Handel)
        // ════════════════════════════════════════════════════════════
        private async Task LoadSaldoKlientaAsync(int klientId, CancellationToken ct)
        {
            txtSaldoInfo.Text = "Ładowanie...";
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync(ct);

                const string sql = @"
                    WITH PNAgg AS (
                        SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl,0)) AS KwotaRozliczona, MAX(PN.Termin) AS TerminPrawdziwy
                        FROM [HANDEL].[HM].[PN] PN WITH (NOLOCK) GROUP BY PN.dkid
                    )
                    SELECT
                      CAST(SUM(DK.walbrutto - ISNULL(PA.KwotaRozliczona, 0)) AS DECIMAL(18,2)) AS DoZaplaty,
                      CAST(SUM(CASE WHEN GETDATE() > ISNULL(PA.TerminPrawdziwy, DK.plattermin)
                                    THEN (DK.walbrutto - ISNULL(PA.KwotaRozliczona, 0)) ELSE 0 END) AS DECIMAL(18,2)) AS Przeterminowane,
                      MAX(CASE WHEN GETDATE() > ISNULL(PA.TerminPrawdziwy, DK.plattermin)
                               THEN DATEDIFF(day, ISNULL(PA.TerminPrawdziwy, DK.plattermin), GETDATE()) ELSE 0 END) AS MaxDni
                    FROM [HANDEL].[HM].[DK] DK WITH (NOLOCK)
                    LEFT JOIN PNAgg PA ON PA.dkid = DK.id
                    WHERE DK.khid = @KlientId AND DK.anulowany = 0
                      AND (DK.walbrutto - ISNULL(PA.KwotaRozliczona, 0)) > 0.01";

                await using var cmd = new SqlCommand(sql, cn);
                cmd.CommandTimeout = 8;
                cmd.Parameters.AddWithValue("@KlientId", klientId);

                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    decimal doZap = reader.IsDBNull(0) ? 0 : reader.GetDecimal(0);
                    decimal przet = reader.IsDBNull(1) ? 0 : reader.GetDecimal(1);
                    int maxDni    = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2));

                    if (doZap == 0)
                    {
                        txtSaldoInfo.Text = "✓ Klient nie ma zaległych faktur";
                        txtSaldoInfo.Foreground = (Brush)FindResource("Brush.Success");
                    }
                    else if (przet > 0)
                    {
                        txtSaldoInfo.Text = $"🔴 {doZap:N0} zł do zapłaty\n   z czego {przet:N0} zł po terminie ({maxDni} dni max)";
                        txtSaldoInfo.Foreground = (Brush)FindResource("Brush.Danger");
                    }
                    else
                    {
                        txtSaldoInfo.Text = $"💛 {doZap:N0} zł do zapłaty (terminowo)";
                        txtSaldoInfo.Foreground = (Brush)FindResource("Brush.WarningText");
                    }
                }
                else
                {
                    txtSaldoInfo.Text = "✓ Brak otwartych faktur";
                    txtSaldoInfo.Foreground = (Brush)FindResource("Brush.TextSecondary");
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                txtSaldoInfo.Text = "(nie udało się pobrać salda)";
                System.Diagnostics.Debug.WriteLine($"LoadSaldo error: {ex.Message}");
            }
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
            txtOdbiorca.Text = "Wybierz zamówienie...";
            txtHandlowiec.Text = "";
            txtDataZamowienia.Text = "";
            _dtDetails.Clear();
            dgDetails.ItemsSource = null;
            btnMarkFakturowane.IsEnabled = false;
            btnCofnijFakturowanie.IsEnabled = false;
            borderTransport.Visibility = Visibility.Collapsed;
            ShowNoChanges();
            txtSaldoInfo.Text = "(wybierz zamówienie)";
            txtSaldoInfo.Foreground = (Brush)FindResource("Brush.TextSecondary");
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
            public decimal TotalIlosc => Info.TotalIlosc;
            public decimal Wartosc    => Info.Wartosc;
            public bool HasTransport => !string.IsNullOrEmpty(Info.GodzWyjazdu);
            public string GodzWyjazdu => Info.GodzWyjazdu ?? "";
            public string Kierowca    => Info.Kierowca ?? "";

            public string CenaDisplay => Info.CzyMaCeny ? "✓" : "✗";
            public Brush  CenaColor   => Info.CzyMaCeny ? _brushSuccess : _brushDanger;

            // Wartość PLN — szara gdy 0 (brak cen)
            public Brush WartoscColor => Info.Wartosc > 0 ? _brushTextDark : _brushTextMuted;

            // Status display + kolory
            public string StatusDisplay
            {
                get
                {
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
