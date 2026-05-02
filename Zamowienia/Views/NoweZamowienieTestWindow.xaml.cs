using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Kalendarz1.Zamowienia.Views
{
    public partial class NoweZamowienieTestWindow : Window
    {
        // ════════════════════ CONSTS ════════════════════
        private const decimal POJEMNIKOW_NA_PALECIE = 36m;
        private const decimal POJEMNIKOW_NA_PALECIE_E2 = 40m;
        private const decimal KG_NA_POJEMNIKU = 15m;
        private const decimal KG_NA_POJEMNIKU_PODROBY = 10m;

        // Podroby (serce, wątroba, żołądki) = 10 kg/poj. Reszta = 15 kg/poj.
        private static decimal KgPerPoj(ProductVm p)
            => p.KategoriaDisplay == "Podroby" ? KG_NA_POJEMNIKU_PODROBY : KG_NA_POJEMNIKU;
        private const int LIMIT_PALET_TIR = 33;
        private const int LIMIT_PALET_SOLOWKA = 18;

        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private readonly CultureInfo _pl = new("pl-PL");

        public string UserID { get; }

        // ════════════════════ STAN ════════════════════
        private int _currentStep = 1;
        private string _aktywnyKatalog = "67095";

        private readonly List<KontrahentVm> _kontrahenci = new();
        private readonly List<ProductVm> _produkty = new();
        private readonly Dictionary<string, DateTime> _ostatnieZamowieniaKlienta = new();
        private readonly Dictionary<DateTime, decimal> _obciazenieDni = new();
        private readonly List<int> _favoriteIds = new();
        private readonly List<int> _customerHours = new();
        private readonly HashSet<string> _userHandlowcy = new(StringComparer.OrdinalIgnoreCase);

        private KontrahentVm? _wybranyKlient;
        private DateTime _wybranaData = DateTime.Today.AddDays(1);
        private TimeSpan _wybranaGodzina = new(8, 0, 0);
        private DateTime _dataProdukcji = DateTime.Today;
        private bool _uiReady;

        public NoweZamowienieTestWindow(string userId)
        {
            UserID = userId ?? "";
            InitializeComponent();
            WindowIconHelper.SetIcon(this);

            LblUser.Text = $"👤 {App.UserFullName ?? UserID}";

            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape) { Close(); }
            };
        }

        // ════════════════════ ŁADOWANIE ════════════════════

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _uiReady = true;

                if (DateTime.Today.DayOfWeek == DayOfWeek.Friday)
                    _wybranaData = DateTime.Today.AddDays(3);

                await LoadUserHandlowcyAsync();
                await LoadKontrahenciAsync();
                await LoadOstatnieZamowieniaAsync();
                await LoadObciazeniaDniAsync();
                await LoadProductsAsync();
                await LoadProductImagesAsync();

                BuildHandlowiecCombo();
                RenderCustomers();
                RenderDaysProd();
                RenderDays();
                RenderHours();
                UpdateValidation();
                UpdateTermDisplay();
                RebuildCart();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd ładowania: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadKontrahenciAsync()
        {
            const string sql = @"
                SELECT c.Id, c.Shortcut AS Nazwa, c.NIP,
                    poa.Postcode AS KodPocztowy, poa.Street AS Miejscowosc,
                    wym.CDim_Handlowiec_Val AS Handlowiec
                FROM [HANDEL].[SSCommon].[STContractors] c
                LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] poa
                    ON poa.ContactGuid = c.ContactGuid AND poa.AddressName = N'adres domyślny'
                LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym ON c.Id = wym.ElementId
                ORDER BY c.Shortcut;";

            _kontrahenci.Clear();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            await using var cmd = new SqlCommand(sql, cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                _kontrahenci.Add(new KontrahentVm
                {
                    Id = rd["Id"]?.ToString() ?? "",
                    Nazwa = rd["Nazwa"]?.ToString() ?? "",
                    NIP = rd["NIP"]?.ToString() ?? "",
                    KodPocztowy = rd["KodPocztowy"]?.ToString() ?? "",
                    Miejscowosc = rd["Miejscowosc"]?.ToString() ?? "",
                    Handlowiec = rd["Handlowiec"]?.ToString() ?? ""
                });
            }
        }

        private async Task LoadOstatnieZamowieniaAsync()
        {
            _ostatnieZamowieniaKlienta.Clear();
            // Tylko zamówienia bieżącego użytkownika (jego "ostatni klienci")
            const string sql = @"
                SELECT KlientId, MAX(DataPrzyjazdu) AS Last
                FROM dbo.ZamowieniaMieso
                WHERE IdUser = @uid
                GROUP BY KlientId";
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                cmd.Parameters.AddWithValue("@uid", UserID ?? "");
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    string kid = rd["KlientId"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(kid) && rd["Last"] != DBNull.Value)
                    {
                        _ostatnieZamowieniaKlienta[kid] = Convert.ToDateTime(rd["Last"]);
                    }
                }
            }
            catch { }

            // load credit limits in batch (best effort)
            try
            {
                const string limitSql = "SELECT id, ISNULL(LimitAmount,0) AS L FROM [HANDEL].[SSCommon].[STContractors]";
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(limitSql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    string kid = rd["id"]?.ToString() ?? "";
                    decimal limit = Convert.ToDecimal(rd["L"]);
                    var k = _kontrahenci.FirstOrDefault(x => x.Id == kid);
                    if (k != null) k.LimitKredytowy = limit;
                }
            }
            catch { }

            foreach (var k in _kontrahenci)
            {
                if (_ostatnieZamowieniaKlienta.TryGetValue(k.Id, out var dt))
                    k.OstatnieZamowienie = dt;
            }
        }

        private async Task LoadObciazeniaDniAsync()
        {
            _obciazenieDni.Clear();
            try
            {
                const string sql = @"
                    SELECT CAST(DataPrzyjazdu AS DATE) AS D, ISNULL(SUM(LiczbaPalet),0) AS Pal
                    FROM dbo.ZamowieniaMieso
                    WHERE DataPrzyjazdu >= CAST(GETDATE() AS DATE)
                      AND DataPrzyjazdu < DATEADD(DAY, 14, CAST(GETDATE() AS DATE))
                    GROUP BY CAST(DataPrzyjazdu AS DATE)";
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    DateTime d = Convert.ToDateTime(rd["D"]);
                    decimal pal = Convert.ToDecimal(rd["Pal"]);
                    _obciazenieDni[d.Date] = pal;
                }
            }
            catch { }
        }

        private async Task LoadProductsAsync()
        {
            _produkty.Clear();
            var excluded = new HashSet<string> { "KURCZAK B", "FILET C" };
            var priorityOrder = new Dictionary<string, int>
            {
                { "KURCZAK A", 1 }, { "FILET A", 2 }, { "ĆWIARTKA", 3 }, { "SKRZYDŁO I", 4 },
                { "NOGA", 5 }, { "PAŁKA", 6 }, { "KORPUS", 7 }, { "POLĘDWICZKI", 8 },
                { "SERCE", 9 }, { "WĄTROBA", 10 }, { "ŻOŁĄDKI", 11 }, { "ĆWIARTKA II", 12 },
                { "FILET II", 13 }, { "FILET II PP", 14 }, { "SKRZYDŁO II", 15 }
            };

            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            foreach (var katalog in new[] { "67095", "67153" })
            {
                await using var cmd = new SqlCommand("SELECT Id, Kod FROM [HANDEL].[HM].[TW] WHERE katalog = @k ORDER BY Kod ASC", cn);
                cmd.Parameters.AddWithValue("@k", katalog);
                await using var rd = await cmd.ExecuteReaderAsync();
                var temp = new List<(int Id, string Kod, int Pri)>();
                while (await rd.ReadAsync())
                {
                    var kod = rd.GetString(1);
                    if (excluded.Any(x => kod.ToUpper().Contains(x))) continue;
                    int pri = int.MaxValue;
                    foreach (var kvp in priorityOrder)
                        if (kod.ToUpper().Contains(kvp.Key)) { pri = kvp.Value; break; }
                    temp.Add((rd.GetInt32(0), kod, pri));
                }
                foreach (var t in temp.OrderBy(x => x.Pri).ThenBy(x => x.Kod))
                {
                    var kat = DetectKategoria(t.Kod);
                    _produkty.Add(new ProductVm
                    {
                        Id = t.Id,
                        Kod = t.Kod,
                        Katalog = katalog,
                        KategoriaDisplay = kat,
                        PlaceholderEmoji = EmojiForKategoria(kat)
                    });
                }
            }
        }

        private async Task LoadProductImagesAsync()
        {
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                await using (var c = new SqlCommand(
                    "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TowarZdjecia') THEN 1 ELSE 0 END", cn))
                {
                    if ((int)(await c.ExecuteScalarAsync())! == 0) return;
                }

                const string sql = "SELECT TowarId, Zdjecie FROM dbo.TowarZdjecia WHERE Aktywne = 1";
                await using var cmd = new SqlCommand(sql, cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                var images = new Dictionary<int, ImageSource>();
                while (await rd.ReadAsync())
                {
                    int id = rd.GetInt32(0);
                    if (rd.IsDBNull(1)) continue;
                    try
                    {
                        byte[] data = (byte[])rd[1];
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = new MemoryStream(data);
                        bi.DecodePixelWidth = 240;
                        bi.EndInit();
                        bi.Freeze();
                        images[id] = bi;
                    }
                    catch { }
                }
                foreach (var p in _produkty)
                {
                    if (images.TryGetValue(p.Id, out var img))
                    {
                        p.ImageSource = img;
                        p.HasImageVisibility = Visibility.Visible;
                        p.PlaceholderVisibility = Visibility.Collapsed;
                    }
                }
            }
            catch { }
        }

        // ════════════════════ STEPPER ════════════════════

        private void BtnStep1_Click(object sender, RoutedEventArgs e) => GoToStep(1);
        private void BtnStep2_Click(object sender, RoutedEventArgs e) { if (_wybranyKlient != null) GoToStep(2); }
        private void BtnStep3_Click(object sender, RoutedEventArgs e) { if (_wybranyKlient != null) GoToStep(3); }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 1 && _wybranyKlient == null) { ShowToast("Wybierz klienta", false); return; }
            if (_currentStep < 3) GoToStep(_currentStep + 1);
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1) GoToStep(_currentStep - 1);
        }

        private void GoToStep(int step)
        {
            int previous = _currentStep;
            _currentStep = step;
            PanelStep1.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
            PanelStep2.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
            PanelStep3.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;

            // checked: krok zakończony (step > circleNum); active: bieżący; pending: jeszcze nieosiągnięty
            UpdateStepCircle(Step1Circle, Step1Num, completed: step > 1, active: step == 1, defaultLabel: "👤");
            UpdateStepCircle(Step2Circle, Step2Num, completed: step > 2, active: step == 2, defaultLabel: "📅");
            UpdateStepCircle(Step3Circle, Step3Num, completed: false,    active: step == 3, defaultLabel: "🛒");

            BtnPrev.Visibility = step > 1 ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Visibility = step < 3 ? Visibility.Visible : Visibility.Collapsed;

            if (step == 3) RenderProducts();

            // smooth fade-in dla aktywnego panelu
            FrameworkElement? activePanel = step == 1 ? PanelStep1 : step == 2 ? PanelStep2 : PanelStep3;
            if (activePanel != null && previous != step)
            {
                activePanel.Opacity = 0;
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                activePanel.BeginAnimation(OpacityProperty, anim);
            }
        }

        private void UpdateStepCircle(Border circle, TextBlock num, bool completed, bool active, string defaultLabel)
        {
            if (active)
            {
                var lg = new LinearGradientBrush(
                    (Color)ColorConverter.ConvertFromString("#6BA044")!,
                    (Color)ColorConverter.ConvertFromString("#46682C")!,
                    new Point(0, 0), new Point(0, 1));
                circle.Background = lg;
                circle.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 12,
                    ShadowDepth = 2,
                    Color = (Color)ColorConverter.ConvertFromString("#46682C")!,
                    Opacity = 0.40
                };
                num.Foreground = Brushes.White;
                num.Text = defaultLabel;
                num.FontSize = 15;
            }
            else if (completed)
            {
                circle.Background = (Brush)FindResource("BrandGreen");
                circle.Effect = null;
                num.Foreground = Brushes.White;
                num.Text = "✓";
                num.FontSize = 16;
            }
            else
            {
                circle.Background = (Brush)new BrushConverter().ConvertFrom("#E5E7EB")!;
                circle.Effect = null;
                num.Foreground = (Brush)FindResource("TextSecondary");
                num.Text = defaultLabel;
                num.FontSize = 15;
            }
        }

        // ════════════════════ KROK 1: KLIENT ════════════════════

        private async Task LoadUserHandlowcyAsync()
        {
            _userHandlowcy.Clear();
            if (string.IsNullOrWhiteSpace(UserID)) return;
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var cmd = new SqlCommand("SELECT HandlowiecName FROM UserHandlowcy WHERE UserID = @uid", cn);
                cmd.Parameters.AddWithValue("@uid", UserID);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var h = rd["HandlowiecName"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(h)) _userHandlowcy.Add(h!);
                }
            }
            catch { }
        }

        private void BuildHandlowiecCombo()
        {
            var hands = _kontrahenci.Select(k => k.Handlowiec).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList();
            CmbHandlowiec.Items.Clear();
            // Domyślnie filtr na handlowców usera, jeśli ma jakichś przypisanych
            if (_userHandlowcy.Count > 0)
                CmbHandlowiec.Items.Add($"⭐ Moi handlowcy ({_userHandlowcy.Count})");
            CmbHandlowiec.Items.Add("— Wszyscy handlowcy —");
            foreach (var h in hands) CmbHandlowiec.Items.Add(h);
            CmbHandlowiec.SelectedIndex = 0; // "Moi handlowcy" jeśli istnieją, inaczej "Wszyscy"
        }

        private void RenderCustomers()
        {
            string filter = (TxtCustSearch.Text ?? "").Trim().ToLowerInvariant();
            string sel = CmbHandlowiec.SelectedItem?.ToString() ?? "";
            bool myHandlowcyMode = sel.StartsWith("⭐");
            bool allMode = sel.StartsWith("—");
            string handFilter = (!myHandlowcyMode && !allMode) ? sel : "";

            IEnumerable<KontrahentVm> q = _kontrahenci;

            if (myHandlowcyMode && _userHandlowcy.Count > 0)
                q = q.Where(k => _userHandlowcy.Contains(k.Handlowiec));
            else if (!string.IsNullOrEmpty(handFilter))
                q = q.Where(k => k.Handlowiec == handFilter);

            if (!string.IsNullOrEmpty(filter))
            {
                q = q.Where(k =>
                    k.Nazwa.ToLowerInvariant().Contains(filter) ||
                    k.NIP.ToLowerInvariant().Contains(filter) ||
                    k.Miejscowosc.ToLowerInvariant().Contains(filter));
            }
            else
            {
                // Bez wyszukiwania: posortuj — najpierw klienci z historią (od najświeższej), potem reszta
                q = q.OrderByDescending(k => k.OstatnieZamowienie ?? DateTime.MinValue)
                     .ThenBy(k => k.Nazwa);
            }

            var list = q.Take(200).ToList();
            list.ForEach(RecalcCustomerBadge);

            ListCustomers.ItemsSource = list;

            LblCustListTitle.Text = string.IsNullOrEmpty(filter)
                ? (myHandlowcyMode ? "Moi klienci" : (allMode ? "Wszyscy klienci" : $"Klienci · {handFilter}"))
                : "Wyniki wyszukiwania";
            LblCustListCount.Text = $"{list.Count} {(list.Count == 1 ? "wynik" : list.Count > 1 && list.Count < 5 ? "wyniki" : "wyników")}";
            TxtCustPlaceholder.Visibility = string.IsNullOrEmpty(TxtCustSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        // Paleta gradientów dla avatarów — wybierana po hashu nazwy
        private static readonly (string from, string to)[] _avatarPalette = new[]
        {
            ("#6BA044", "#46682C"), // green
            ("#3B82F6", "#1E40AF"), // blue
            ("#8B5CF6", "#5B21B6"), // violet
            ("#EC4899", "#9D174D"), // pink
            ("#F59E0B", "#B45309"), // amber
            ("#10B981", "#065F46"), // emerald
            ("#06B6D4", "#0E7490"), // cyan
            ("#EF4444", "#991B1B"), // red
            ("#0EA5E9", "#075985"), // sky
            ("#A855F7", "#6B21A8"), // purple
        };

        private static Brush MakeAvatarGradient(string text)
        {
            int hash = 0;
            foreach (char c in text) hash = (hash * 31 + c) & 0x7FFFFFFF;
            var (from, to) = _avatarPalette[hash % _avatarPalette.Length];
            var lg = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            lg.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(from)!, 0));
            lg.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(to)!, 1));
            lg.Freeze();
            return lg;
        }

        private static string GetInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(new[] { ' ', '.', ',', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
                return (parts[0][0].ToString() + parts[1][0]).ToUpperInvariant();
            return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
        }

        private void RecalcCustomerBadge(KontrahentVm k)
        {
            if (k.LimitKredytowy <= 0)
            {
                k.LimitBadge = "—";
                k.LimitBadgeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")!);
                k.LimitBadgeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")!);
            }
            else
            {
                k.LimitBadge = "OK";
                k.LimitBadgeBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7")!);
                k.LimitBadgeFg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#166534")!);
            }

            if (k.OstatnieZamowienie.HasValue)
            {
                int days = (DateTime.Today - k.OstatnieZamowienie.Value.Date).Days;
                k.LastOrderDisplay = days switch
                {
                    0 => "Ostatnio: dziś",
                    1 => "Ostatnio: wczoraj",
                    < 7 => $"Ostatnio: {days} dni temu",
                    < 30 => $"Ostatnio: {days / 7} tyg. temu",
                    _ => $"Ostatnio: {k.OstatnieZamowienie.Value:dd.MM.yyyy}"
                };
                k.LastOrderShort = days switch
                {
                    0 => "dziś",
                    1 => "wczoraj",
                    < 7 => $"{days}d temu",
                    < 30 => $"{days / 7}tyg temu",
                    _ => k.OstatnieZamowienie.Value.ToString("dd.MM")
                };
            }
            else
            {
                k.LastOrderDisplay = "Brak historii";
                k.LastOrderShort = "—";
            }

            k.NipDisplay = string.IsNullOrEmpty(k.NIP) ? "" : "NIP " + k.NIP;
            k.Initials = GetInitials(k.Nazwa);
            k.AvatarBrush = MakeAvatarGradient(k.Nazwa);
            k.HandlowiecShort = string.IsNullOrEmpty(k.Handlowiec) ? "—"
                : (k.Handlowiec.Length > 10 ? k.Handlowiec.Substring(0, 10) + "…" : k.Handlowiec);
        }

        private void TxtCustSearch_TextChanged(object sender, TextChangedEventArgs e) => RenderCustomers();
        private void CmbHandlowiec_SelectionChanged(object sender, SelectionChangedEventArgs e) => RenderCustomers();

        private async void BtnCustomerCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string id)
            {
                var k = _kontrahenci.FirstOrDefault(x => x.Id == id);
                if (k != null)
                {
                    _wybranyKlient = k;
                    await ApplySelectedCustomerAsync();
                    GoToStep(2);
                }
            }
        }

        private async Task ApplySelectedCustomerAsync()
        {
            if (_wybranyKlient == null) return;

            CustomerStrip.Visibility = Visibility.Visible;
            // krótka animacja slide-in
            var slideIn = new DoubleAnimation(-30, 0, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            CustomerStrip.RenderTransform = new TranslateTransform();
            CustomerStrip.RenderTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);
            CustomerStrip.Opacity = 0;
            CustomerStrip.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280)));

            CustName.Text = _wybranyKlient.Nazwa;
            string nip = _wybranyKlient.NIP ?? "";
            CustNip.Text = string.IsNullOrEmpty(nip) ? "" : "NIP " + nip;
            CustNipSep.Visibility = string.IsNullOrEmpty(nip) ? Visibility.Collapsed : Visibility.Visible;
            CustAddress.Text = $"{_wybranyKlient.KodPocztowy} {_wybranyKlient.Miejscowosc}".Trim();
            CustHandlowiec.Text = string.IsNullOrEmpty(_wybranyKlient.Handlowiec) ? "—" : _wybranyKlient.Handlowiec;
            CustInitials.Text = GetInitials(_wybranyKlient.Nazwa);
            CustAvatarBorder.Background = MakeAvatarGradient(_wybranyKlient.Nazwa);

            // Header chip
            HeaderChip.Visibility = Visibility.Visible;
            ChipName.Text = _wybranyKlient.Nazwa;
            ChipInitials.Text = GetInitials(_wybranyKlient.Nazwa);
            ChipAvatar.Background = MakeAvatarGradient(_wybranyKlient.Nazwa);

            Step1Sub.Text = _wybranyKlient.Nazwa.Length > 20 ? _wybranyKlient.Nazwa.Substring(0, 20) + "…" : _wybranyKlient.Nazwa;

            await LoadPlatnosciAsync();
            await LoadCustomerPreferencesAsync();
            await LoadFavoritesAsync();
            UpdateValidation();
        }

        private void HeaderChip_Click(object sender, MouseButtonEventArgs e) => BtnChangeCustomer_Click(sender, e);

        private async Task LoadCustomerPreferencesAsync()
        {
            if (_wybranyKlient == null) return;
            int kid;
            if (!int.TryParse(_wybranyKlient.Id, out kid)) return;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();

                // Top 6 najczęściej wybieranych godzin przyjazdu (pomijamy 0 = nieustawione)
                _customerHours.Clear();
                var cmdH = new SqlCommand(@"
                    SELECT TOP 6 DATEPART(HOUR, DataPrzyjazdu) AS H, COUNT(*) AS C
                    FROM dbo.ZamowieniaMieso
                    WHERE KlientId = @kid AND DataPrzyjazdu IS NOT NULL
                      AND DATEPART(HOUR, DataPrzyjazdu) > 0
                    GROUP BY DATEPART(HOUR, DataPrzyjazdu)
                    ORDER BY COUNT(*) DESC, DATEPART(HOUR, DataPrzyjazdu) ASC", cn);
                cmdH.Parameters.AddWithValue("@kid", kid);
                using (var rdH = await cmdH.ExecuteReaderAsync())
                {
                    while (await rdH.ReadAsync())
                    {
                        int h = Convert.ToInt32(rdH["H"]);
                        if (h > 0 && h < 24) _customerHours.Add(h);
                    }
                }
                if (_customerHours.Count > 0)
                    _wybranyKlient.PreferredHour = _customerHours[0];

                // Najczęstszy odstęp dni między datą produkcji a datą odbioru
                var cmdD = new SqlCommand(@"
                    SELECT TOP 1 DATEDIFF(DAY, DataProdukcji, CAST(DataPrzyjazdu AS DATE)) AS Diff
                    FROM dbo.ZamowieniaMieso
                    WHERE KlientId = @kid
                      AND DataProdukcji IS NOT NULL
                      AND DataPrzyjazdu IS NOT NULL
                      AND DATEDIFF(DAY, DataProdukcji, CAST(DataPrzyjazdu AS DATE)) BETWEEN 0 AND 7
                    GROUP BY DATEDIFF(DAY, DataProdukcji, CAST(DataPrzyjazdu AS DATE))
                    ORDER BY COUNT(*) DESC", cn);
                cmdD.Parameters.AddWithValue("@kid", kid);
                var dObj = await cmdD.ExecuteScalarAsync();
                if (dObj != null && dObj != DBNull.Value)
                    _wybranyKlient.PreferredDeliveryDiff = Convert.ToInt32(dObj);
            }
            catch { }

            // Zastosuj preferencje
            if (_wybranyKlient.PreferredHour is int hr && hr > 0)
            {
                _wybranaGodzina = new TimeSpan(hr, 0, 0);
                TxtCustomHour.Text = $"{hr:00}:00";
            }

            int diff = _wybranyKlient.PreferredDeliveryDiff ?? 1;
            if (diff < 0) diff = 1;
            var newDelivery = _dataProdukcji.AddDays(diff);
            while (newDelivery.DayOfWeek == DayOfWeek.Sunday) newDelivery = newDelivery.AddDays(1);
            _wybranaData = newDelivery;

            RenderDaysProd();
            RenderDays();
            RenderHours();
            UpdateTermDisplay();
        }

        private async Task LoadPlatnosciAsync()
        {
            if (_wybranyKlient == null) return;
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                var cmd1 = new SqlCommand("SELECT ISNULL(LimitAmount,0) FROM [HANDEL].[SSCommon].[STContractors] WHERE id=@id", cn);
                cmd1.Parameters.AddWithValue("@id", int.Parse(_wybranyKlient.Id));
                var limit = Convert.ToDecimal(await cmd1.ExecuteScalarAsync() ?? 0);

                var cmd2 = new SqlCommand(@"
                    WITH PNAgg AS (SELECT PN.dkid, SUM(ISNULL(PN.kwotarozl,0)) AS KR FROM [HANDEL].[HM].[PN] PN GROUP BY PN.dkid)
                    SELECT ISNULL(SUM(DK.walbrutto - ISNULL(PA.KR, 0)), 0)
                    FROM [HANDEL].[HM].[DK] DK
                    LEFT JOIN PNAgg PA ON PA.dkid = DK.id
                    WHERE DK.khid = @id AND DK.anulowany = 0 AND (DK.walbrutto - ISNULL(PA.KR, 0)) > 0", cn);
                cmd2.Parameters.AddWithValue("@id", int.Parse(_wybranyKlient.Id));
                var dluzny = Convert.ToDecimal(await cmd2.ExecuteScalarAsync() ?? 0);

                _wybranyKlient.LimitKredytowy = limit;
                _wybranyKlient.DoZaplacenia = dluzny;

                UpdateLimitDisplay();
            }
            catch
            {
                HdrLblLimitHint.Text = "Błąd odczytu limitu";
            }
        }

        private void BtnChangeCustomer_Click(object sender, RoutedEventArgs e)
        {
            _wybranyKlient = null;
            CustomerStrip.Visibility = Visibility.Collapsed;
            HeaderChip.Visibility = Visibility.Collapsed;
            Step1Sub.Text = "Wybierz odbiorcę";
            HdrLimitPctLabel.Text = "—";
            HdrProgressLimit.Width = 0;
            HdrLblLimitHint.Text = "Wybierz klienta";
            ChipPalety.Text = "0/33";
            ChipLimit.Text = "—";
            ChipTermin.Text = "—";
            _favoriteIds.Clear();
            _customerHours.Clear();
            FavoritesPanel.Visibility = Visibility.Collapsed;
            RenderHours();
            GoToStep(1);
            UpdateValidation();
        }

        // ════════════════════ KROK 2: TERMIN ════════════════════

        private void RenderDays()
        {
            var days = new List<DayVm>();
            DateTime start = DateTime.Today;
            int added = 0;
            int offset = 0;
            while (added < 7)
            {
                DateTime d = start.AddDays(offset++);
                if (d.DayOfWeek == DayOfWeek.Sunday) continue;
                _obciazenieDni.TryGetValue(d, out decimal pal);
                bool isSelected = d.Date == _wybranaData.Date;
                double loadPct = Math.Min(1.0, (double)pal / LIMIT_PALET_TIR);
                days.Add(new DayVm
                {
                    Date = d,
                    DayName = _pl.DateTimeFormat.GetAbbreviatedDayName(d.DayOfWeek).ToUpperInvariant(),
                    DayNum = d.Day.ToString(),
                    MonthShort = _pl.DateTimeFormat.GetAbbreviatedMonthName(d.Month).ToUpperInvariant(),
                    LoadDisplay = pal == 0 ? "wolne" : $"{pal:N0} pal",
                    IsSelected = isSelected,
                    BgBrush = isSelected ? (Brush)FindResource("BrandGreenLight") : Brushes.White,
                    BorderBrush = isSelected ? (Brush)FindResource("BrandGreen") : (Brush)FindResource("Border"),
                    ForeBrush = isSelected ? (Brush)FindResource("BrandGreenDark") : (Brush)FindResource("TextPrimary"),
                    LoadBrush = pal >= LIMIT_PALET_TIR ? (Brush)FindResource("Danger") : pal >= 20 ? (Brush)FindResource("Warning") : (Brush)FindResource("BrandGreen"),
                    LoadBarWidth = Math.Max(2, 50 * loadPct)
                });
                added++;
            }
            ListDays.ItemsSource = days;
            LblOdbiorSelected.Text = $"{_wybranaData:dd.MM.yyyy} ({_pl.DateTimeFormat.GetDayName(_wybranaData.DayOfWeek)})";
        }

        private void RenderDaysProd()
        {
            var days = new List<DayVm>();
            DateTime start = DateTime.Today;
            int added = 0;
            int offset = 0;
            while (added < 7)
            {
                DateTime d = start.AddDays(offset++);
                if (d.DayOfWeek == DayOfWeek.Sunday) continue;
                bool isSelected = d.Date == _dataProdukcji.Date;
                days.Add(new DayVm
                {
                    Date = d,
                    DayName = _pl.DateTimeFormat.GetAbbreviatedDayName(d.DayOfWeek).ToUpperInvariant(),
                    DayNum = d.Day.ToString(),
                    MonthShort = _pl.DateTimeFormat.GetAbbreviatedMonthName(d.Month).ToUpperInvariant(),
                    LoadDisplay = "",
                    IsSelected = isSelected,
                    BgBrush = isSelected ? (Brush)FindResource("BrandGreenLight") : Brushes.White,
                    BorderBrush = isSelected ? (Brush)FindResource("BrandGreen") : (Brush)FindResource("Border"),
                    ForeBrush = isSelected ? (Brush)FindResource("BrandGreenDark") : (Brush)FindResource("TextPrimary"),
                    LoadBrush = (Brush)FindResource("TextMuted"),
                    LoadBarWidth = 0
                });
                added++;
            }
            ListDaysProd.ItemsSource = days;
            LblProdSelected.Text = $"{_dataProdukcji:dd.MM.yyyy} ({_pl.DateTimeFormat.GetDayName(_dataProdukcji.DayOfWeek)})";
        }

        private void BtnDayProdCard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is DateTime d)
            {
                _dataProdukcji = d;
                // Auto-dobranie daty odbioru bazując na preferencji klienta
                int diff = _wybranyKlient?.PreferredDeliveryDiff ?? 1;
                if (diff < 0) diff = 0;
                var newDelivery = _dataProdukcji.AddDays(diff);
                // pomiń niedzielę
                while (newDelivery.DayOfWeek == DayOfWeek.Sunday) newDelivery = newDelivery.AddDays(1);
                _wybranaData = newDelivery;
                RenderDaysProd();
                RenderDays();
                UpdateTermDisplay();
                UpdateValidation();
            }
        }

        private void BtnDayCard_Click(object sender, RoutedEventArgs e)
        {
            // klikanie daty odbioru NIE może modyfikować daty produkcji - to są niezależne pola
            if (sender is Button b && b.Tag is DateTime d)
            {
                _wybranaData = d;
                RenderDays();
                UpdateTermDisplay();
                UpdateValidation();
            }
        }

        private static readonly int[] _domyslneGodziny = { 6, 8, 10, 12, 14, 16 };

        private void RenderHours()
        {
            // Użyj godzin klienta (top N) jeśli są — inaczej fallback do 6/8/10/12/14/16
            var sourceHours = _customerHours.Count > 0
                ? _customerHours.OrderBy(h => h).ToList()
                : _domyslneGodziny.ToList();

            // Upewnij się że aktualnie wybrana godzina jest w liście (żeby chip pokazał selekcję)
            int selH = _wybranaGodzina.Hours;
            if (selH > 0 && !sourceHours.Contains(selH))
            {
                sourceHours.Add(selH);
                sourceHours = sourceHours.OrderBy(h => h).ToList();
            }

            var hours = new List<HourVm>();
            foreach (int h in sourceHours)
            {
                var time = new TimeSpan(h, 0, 0);
                bool selected = _wybranaGodzina == time;
                hours.Add(new HourVm
                {
                    Hour = time,
                    HourDisplay = $"{h:00}:00",
                    BgBrush = selected ? (Brush)FindResource("BrandGreen") : Brushes.White,
                    BorderBrush = selected ? (Brush)FindResource("BrandGreen") : (Brush)FindResource("Border"),
                    ForeBrush = selected ? Brushes.White : (Brush)FindResource("TextPrimary")
                });
            }
            ListHours.ItemsSource = hours;
        }

        private void BtnHourChip_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is TimeSpan t)
            {
                _wybranaGodzina = t;
                TxtCustomHour.Text = $"{t.Hours:00}:{t.Minutes:00}";
                RenderHours();
                UpdateTermDisplay();
            }
        }

        private void TxtCustomHour_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyCustomHour(showToast: true);
        }

        private void TxtCustomHour_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_uiReady) return;
            ApplyCustomHour(showToast: false);
        }
        private void TxtCustomHour_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!_uiReady) return;
            ApplyCustomHour(showToast: false);
        }

        private void ApplyCustomHour(bool showToast)
        {
            if (TimeSpan.TryParse(TxtCustomHour.Text, out var t))
            {
                _wybranaGodzina = t;
                RenderHours();
                UpdateTermDisplay();
                if (showToast) ShowToast($"✓ Godzina ustawiona: {t.Hours:00}:{t.Minutes:00}", true);
            }
        }

        // ── DatePicker handlery (alternatywa dla kart dni) ──
        private void DpProd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpProd.SelectedDate is DateTime d && d.Date != _dataProdukcji.Date)
            {
                _dataProdukcji = d.Date;
                int diff = _wybranyKlient?.PreferredDeliveryDiff ?? 1;
                if (diff < 0) diff = 1;
                var newDelivery = _dataProdukcji.AddDays(diff);
                while (newDelivery.DayOfWeek == DayOfWeek.Sunday) newDelivery = newDelivery.AddDays(1);
                _wybranaData = newDelivery;
                RenderDaysProd();
                RenderDays();
                UpdateTermDisplay();
            }
        }

        private void DpOdbior_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DpOdbior.SelectedDate is DateTime d && d.Date != _wybranaData.Date)
            {
                _wybranaData = d.Date;
                RenderDays();
                UpdateTermDisplay();
            }
        }

        private bool _suppressTransportSync;
        private void ChkWlasnyOdbior_Changed(object sender, RoutedEventArgs e)
        {
            LblGodzinaHeader.Text = ChkWlasnyOdbior.IsChecked == true ? "Godzina odbioru" : "Godzina przyjazdu";
            if (_suppressTransportSync) return;
            _suppressTransportSync = true;
            ChkSidebarWlasny.IsChecked = ChkWlasnyOdbior.IsChecked;
            _suppressTransportSync = false;
        }

        private void ChkSidebarWlasny_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressTransportSync) return;
            _suppressTransportSync = true;
            ChkWlasnyOdbior.IsChecked = ChkSidebarWlasny.IsChecked;
            _suppressTransportSync = false;
            LblGodzinaHeader.Text = ChkWlasnyOdbior.IsChecked == true ? "Godzina odbioru" : "Godzina przyjazdu";
        }

        private void UpdateTermDisplay()
        {
            string day = _pl.DateTimeFormat.GetDayName(_wybranaData.DayOfWeek);
            HdrTerminMain.Text = $"{_wybranaData:dd.MM.yyyy}";
            HdrTerminSub.Text = $"{day} · {_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}";
            Step2Sub.Text = $"{_wybranaData:dd.MM} · {_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}";
            ChipTermin.Text = $"{_wybranaData:dd.MM} {_wybranaGodzina.Hours:00}:{_wybranaGodzina.Minutes:00}";

            string prodDay = _pl.DateTimeFormat.GetDayName(_dataProdukcji.DayOfWeek);
            SbProdMain.Text = $"{_dataProdukcji:dd.MM.yyyy}";
            SbProdSub.Text = prodDay;

            // Sync DatePickers (bez wywołania ich SelectionChanged dzięki check d != current w handlerach)
            if (DpProd.SelectedDate?.Date != _dataProdukcji.Date) DpProd.SelectedDate = _dataProdukcji;
            if (DpOdbior.SelectedDate?.Date != _wybranaData.Date) DpOdbior.SelectedDate = _wybranaData;
        }

        // ════════════════════ KROK 3: POZYCJE ════════════════════

        private void RenderProducts()
        {
            var visible = _produkty.Where(p => p.Katalog == _aktywnyKatalog).ToList();
            foreach (var p in visible)
            {
                RecalcProductDisplay(p);
            }

            // Najczęściej kupowane (zachowując kolejność popularności)
            List<ProductVm> favs = new();
            if (_favoriteIds.Count > 0)
            {
                favs = _favoriteIds
                    .Select(id => visible.FirstOrDefault(p => p.Id == id))
                    .Where(p => p != null)
                    .Cast<ProductVm>()
                    .Take(10)
                    .ToList();
            }

            if (favs.Count > 0)
            {
                ListFavorites.ItemsSource = null;
                ListFavorites.ItemsSource = favs;
                LblFavoritesCount.Text = $"({favs.Count})";
                FavoritesPanel.Visibility = Visibility.Visible;
            }
            else
            {
                FavoritesPanel.Visibility = Visibility.Collapsed;
            }

            // Wszystkie produkty — z wykluczeniem tych już pokazanych w "Najczęściej kupowane"
            var favIds = new HashSet<int>(favs.Select(p => p.Id));
            var others = visible.Where(p => !favIds.Contains(p.Id)).ToList();
            ListProducts.ItemsSource = null;
            ListProducts.ItemsSource = others;
        }

        private async Task LoadFavoritesAsync()
        {
            _favoriteIds.Clear();
            if (_wybranyKlient == null || !int.TryParse(_wybranyKlient.Id, out int kid)) return;

            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                var cmd = new SqlCommand(@"
                    SELECT TOP 10 t.KodTowaru, COUNT(*) AS Cnt, SUM(t.Ilosc) AS Suma
                    FROM dbo.ZamowieniaMiesoTowar t
                    INNER JOIN dbo.ZamowieniaMieso z ON z.Id = t.ZamowienieId
                    WHERE z.KlientId = @kid AND t.KodTowaru IS NOT NULL
                    GROUP BY t.KodTowaru
                    ORDER BY COUNT(*) DESC, SUM(t.Ilosc) DESC", cn);
                cmd.Parameters.AddWithValue("@kid", kid);
                using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    if (int.TryParse(rd["KodTowaru"]?.ToString(), out int id))
                        _favoriteIds.Add(id);
                }
            }
            catch { }
        }

        private static string EmojiForKategoria(string kat) => kat switch
        {
            "Tuszka" => "🐔",
            "Filet" => "🥩",
            "Ćwiartka" => "🍗",
            "Noga" => "🍗",
            "Skrzydło" => "🪽",
            "Korpus" => "🦴",
            "Polędwiczki" => "🥩",
            "Podroby" => "🫀",
            _ => "🍖"
        };

        private static string DetectKategoria(string kod)
        {
            string u = kod.ToUpperInvariant();
            if (u.Contains("KURCZAK")) return "Tuszka";
            if (u.Contains("FILET")) return "Filet";
            if (u.Contains("ĆWIARTKA") || u.Contains("CWIARTKA")) return "Ćwiartka";
            if (u.Contains("NOGA") || u.Contains("PAŁKA") || u.Contains("PALKA")) return "Noga";
            if (u.Contains("SKRZYDŁO") || u.Contains("SKRZYDLO")) return "Skrzydło";
            if (u.Contains("KORPUS")) return "Korpus";
            if (u.Contains("POLĘDWICZ") || u.Contains("POLEDWICZ")) return "Polędwiczki";
            if (u.Contains("SERCE") || u.Contains("WĄTROBA") || u.Contains("WATROBA") || u.Contains("ŻOŁĄDK") || u.Contains("ZOLADK")) return "Podroby";
            return "Inne";
        }

        private static readonly Brush _highlightBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5C8A3A")!);
        private static readonly Brush _normalBorder = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAEDF1")!);

        // skip == "kg" / "poj" / "pal" → nie nadpisuj pola które użytkownik aktualnie edytuje
        private void RecalcProductDisplay(ProductVm p, string? skip = null)
        {
            decimal kgPoj = KgPerPoj(p);
            decimal pojNaPalecie = p.E2 ? POJEMNIKOW_NA_PALECIE_E2 : POJEMNIKOW_NA_PALECIE;
            p.Pojemniki = p.QtyKg > 0 ? Math.Ceiling(p.QtyKg / kgPoj) : 0;
            p.Palety = p.Pojemniki > 0 ? Math.Round(p.Pojemniki / pojNaPalecie, 2) : 0;

            if (skip != "kg")  p.QtyKgDisplay = p.QtyKg > 0 ? p.QtyKg.ToString("0", _pl) : "";
            if (skip != "poj") p.PojDisplay   = p.Pojemniki > 0 ? p.Pojemniki.ToString("0", _pl) : "";
            if (skip != "pal") p.PalDisplay   = p.Palety > 0 ? p.Palety.ToString("0.##", _pl) : "";
            p.CenaDisplay = p.Cena ?? "";

            bool inCart = p.QtyKg > 0;
            p.InCartVisibility = inCart ? Visibility.Visible : Visibility.Collapsed;
            p.InCartBadge = inCart ? "✓" : "";
            p.ProductBorder = inCart ? _highlightBorder : _normalBorder;
            p.ProductBorderThickness = inCart ? new Thickness(2) : new Thickness(1);

            p.NotifyAll(skip);
        }

        private void BtnTypProduktu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string kat)
            {
                _aktywnyKatalog = kat;
                if (kat == "67095")
                {
                    BtnSwieze.Background = (Brush)FindResource("BrandGreen");
                    BtnSwieze.Foreground = Brushes.White;
                    BtnMrozone.Background = Brushes.Transparent;
                    BtnMrozone.Foreground = (Brush)FindResource("TextSecondary");
                }
                else
                {
                    BtnMrozone.Background = (Brush)FindResource("BrandGreen");
                    BtnMrozone.Foreground = Brushes.White;
                    BtnSwieze.Background = Brushes.Transparent;
                    BtnSwieze.Foreground = (Brush)FindResource("TextSecondary");
                }
                RenderProducts();
            }
        }

        private void TxtProductSearch_TextChanged(object sender, TextChangedEventArgs e) => RenderProducts();

        private void TxtNumeric_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb) tb.SelectAll();
        }

        private static bool TryParseInput(string? text, out decimal value)
            => decimal.TryParse((text ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out value);

        private static void MoveFocusNext(object sender)
        {
            if (sender is UIElement ui)
                ui.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        // Ochrona pola edytowanego: zachowujemy dokładny tekst+kursor, by żaden refresh bindingu
        // nie nadpisał tego co użytkownik wpisuje (działa dla każdego ItemsControl, w tym favoritesów).
        private static void PreserveTypedText(TextBox tb, string userText, int caret)
        {
            if (tb.Text != userText)
            {
                tb.Text = userText;
            }
            int safeCaret = Math.Min(caret, tb.Text.Length);
            if (safeCaret >= 0 && tb.CaretIndex != safeCaret)
                tb.CaretIndex = safeCaret;
        }

        // Flaga blokująca rekurencyjne TextChanged przy programowej aktualizacji rodzeństwa pola
        private bool _internalTextUpdate;

        // Szukaj TextBoxa w całym drzewie wizualnym po Name (FieldKg/FieldPoj/FieldPal) + Tag (Id produktu)
        private static TextBox? FindTextBoxByName(DependencyObject root, int productId, string fieldName)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is TextBox tb && tb.Name == fieldName && tb.Tag is int id && id == productId)
                    return tb;
                var deeper = FindTextBoxByName(child, productId, fieldName);
                if (deeper != null) return deeper;
            }
            return null;
        }

        // Wymusza ustawienie Text na rodzeństwie (z flagą żeby nie wpaść w rekurencję)
        private void ForceSiblingText(TextBox source, string fieldName, string value)
        {
            if (source.Tag is not int productId) return;
            var sib = FindTextBoxByName(this, productId, fieldName);
            if (sib == null || sib == source) return;
            if (sib.Text == value) return;
            _internalTextUpdate = true;
            try { sib.Text = value; }
            finally { _internalTextUpdate = false; }
        }


        // ── KG ──
        private void TxtQty_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_internalTextUpdate) return;
            if (sender is not TextBox tb || tb.Tag is not int id) return;
            string userText = tb.Text ?? "";
            int caret = tb.CaretIndex;

            var p = _produkty.FirstOrDefault(x => x.Id == id);
            if (p == null) return;

            if (string.IsNullOrWhiteSpace(userText))
            {
                p.QtyKg = 0;
            }
            else if (TryParseInput(userText, out var val))
            {
                p.QtyKg = Math.Max(0, val);
            }
            else
            {
                // niepoprawny wpis — nie ruszamy modelu, ale chronimy tekst
                PreserveTypedText(tb, userText, caret);
                return;
            }

            RecalcProductDisplay(p, skip: "kg");
            RebuildCart();
            // Wymuszamy aktualizację POJ i PAL bezpośrednio (nie ufamy bindingowi)
            ForceSiblingText(tb, "FieldPoj", p.PojDisplay);
            ForceSiblingText(tb, "FieldPal", p.PalDisplay);
            PreserveTypedText(tb, userText, caret);
        }
        private void TxtQty_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) MoveFocusNext(sender);
        }
        private void TxtQty_LostFocus(object sender, RoutedEventArgs e) { }

        // ── POJEMNIKI ──
        private void TxtPoj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_internalTextUpdate) return;
            if (sender is not TextBox tb || tb.Tag is not int id) return;
            string userText = tb.Text ?? "";
            int caret = tb.CaretIndex;

            var p = _produkty.FirstOrDefault(x => x.Id == id);
            if (p == null) return;

            if (string.IsNullOrWhiteSpace(userText))
            {
                p.QtyKg = 0;
            }
            else if (TryParseInput(userText, out var poj))
            {
                poj = Math.Max(0, Math.Round(poj));
                p.QtyKg = poj * KgPerPoj(p);
            }
            else
            {
                PreserveTypedText(tb, userText, caret);
                return;
            }

            RecalcProductDisplay(p, skip: "poj");
            RebuildCart();
            // Wymuszamy aktualizację KG i PAL bezpośrednio
            ForceSiblingText(tb, "FieldKg", p.QtyKgDisplay);
            ForceSiblingText(tb, "FieldPal", p.PalDisplay);
            PreserveTypedText(tb, userText, caret);
        }
        private void TxtPoj_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) MoveFocusNext(sender);
        }
        private void TxtPoj_LostFocus(object sender, RoutedEventArgs e) { }

        // ── PALETY ──
        private void TxtPal_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_internalTextUpdate) return;
            if (sender is not TextBox tb || tb.Tag is not int id) return;
            string userText = tb.Text ?? "";
            int caret = tb.CaretIndex;

            var p = _produkty.FirstOrDefault(x => x.Id == id);
            if (p == null) return;

            if (string.IsNullOrWhiteSpace(userText))
            {
                p.QtyKg = 0;
            }
            else if (TryParseInput(userText, out var pal))
            {
                pal = Math.Max(0, pal);
                decimal pojNaPalecie = p.E2 ? POJEMNIKOW_NA_PALECIE_E2 : POJEMNIKOW_NA_PALECIE;
                decimal poj = Math.Round(pal * pojNaPalecie);
                p.QtyKg = poj * KgPerPoj(p);
            }
            else
            {
                PreserveTypedText(tb, userText, caret);
                return;
            }

            RecalcProductDisplay(p, skip: "pal");
            RebuildCart();
            // Wymuszamy aktualizację KG i POJ bezpośrednio
            ForceSiblingText(tb, "FieldKg", p.QtyKgDisplay);
            ForceSiblingText(tb, "FieldPoj", p.PojDisplay);
            PreserveTypedText(tb, userText, caret);
        }
        private void TxtPal_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) MoveFocusNext(sender);
        }
        private void TxtPal_LostFocus(object sender, RoutedEventArgs e) { }

        private void TxtCena_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is int id)
            {
                var p = _produkty.FirstOrDefault(x => x.Id == id);
                if (p != null) p.Cena = tb.Text;
            }
        }

        private void ChkOpcja_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.Tag is int id)
            {
                var p = _produkty.FirstOrDefault(x => x.Id == id);
                if (p != null)
                {
                    RecalcProductDisplay(p);
                    RebuildCart();
                }
            }
        }

        private void BtnCartRemove_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is int id)
            {
                var p = _produkty.FirstOrDefault(x => x.Id == id);
                if (p != null)
                {
                    p.QtyKg = 0;
                    RecalcProductDisplay(p);
                    RebuildCart();
                    if (_currentStep == 3) RenderProducts();
                }
            }
        }

        // ════════════════════ KOSZYK / SUMARYZACJA ════════════════════

        private void RebuildCart()
        {
            var inCart = _produkty.Where(p => p.QtyKg > 0).ToList();

            int pozycji = inCart.Count;
            decimal pojemniki = inCart.Sum(p => p.Pojemniki);
            decimal palety = inCart.Sum(p => p.Palety);
            decimal kg = inCart.Sum(p => p.QtyKg);

            TotPozycje.Text = pozycji.ToString();
            TotPojemniki.Text = pojemniki.ToString("0", _pl);
            TotKg.Text = kg.ToString("N0", _pl) + " kg";
            TotPalety.Text = palety.ToString("0.##", _pl);

            double pct = Math.Min(1.0, (double)palety / LIMIT_PALET_TIR);

            // Pasek palet w panelu klienta (sidebar)
            double headerBarMax = HdrProgressPalety.Parent is Border parentBar && parentBar.ActualWidth > 0
                ? parentBar.ActualWidth : 280;
            HdrTotPalety.Text = palety.ToString("0.##", _pl);
            HdrProgressPalety.Width = Math.Max(0, headerBarMax * pct);

            string paletyHint;
            if (palety <= LIMIT_PALET_SOLOWKA) paletyHint = $"Solówka {palety:0.##}/{LIMIT_PALET_SOLOWKA} ✓";
            else if (palety <= LIMIT_PALET_TIR) paletyHint = $"TIR {palety:0.##}/{LIMIT_PALET_TIR}";
            else paletyHint = $"⚠ Przekroczenie TIR ({palety:0.##}/{LIMIT_PALET_TIR})";
            HdrLblPaletyHint.Text = paletyHint;

            var paletyColor = palety > LIMIT_PALET_TIR ? (Brush)FindResource("Danger")
                : palety > LIMIT_PALET_SOLOWKA ? (Brush)FindResource("Warning")
                : (Brush)FindResource("BrandGreen");
            HdrProgressPalety.Background = paletyColor;

            // Header chip palety
            ChipPalety.Text = $"{palety:0.##}/{LIMIT_PALET_TIR}";

            // cart list
            var cartItems = inCart.Select(p =>
            {
                bool hasCena = decimal.TryParse((p.Cena ?? "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var cenaVal) && cenaVal > 0;
                decimal wartosc = hasCena ? cenaVal * p.QtyKg : 0;
                return new CartItem
                {
                    Id = p.Id,
                    Kod = p.Kod,
                    Detail = $"{p.QtyKg:N0} kg · {p.Pojemniki:0} poj · {p.Palety:0.##} pal",
                    CenaDisplay = hasCena ? $"{cenaVal:N2} zł/kg" : "—",
                    WartoscDisplay = hasCena ? $"{wartosc:N2} zł" : "",
                    CenaVisibility = Visibility.Visible,
                    ImageSource = p.ImageSource,
                    HasImageVisibility = p.HasImageVisibility,
                    PlaceholderVisibility = p.PlaceholderVisibility,
                    PlaceholderEmoji = p.PlaceholderEmoji,
                    IconE2 = "📦",
                    IconE2Visibility = p.E2 ? Visibility.Visible : Visibility.Collapsed,
                    IconFolia = "🧴",
                    IconFoliaVisibility = p.Folia ? Visibility.Visible : Visibility.Collapsed,
                    IconHallal = "🔪",
                    IconHallalVisibility = p.Hallal ? Visibility.Visible : Visibility.Collapsed,
                    IconStrefa = "⚠️",
                    IconStrefaVisibility = p.Strefa ? Visibility.Visible : Visibility.Collapsed
                };
            }).ToList();
            ListCart.ItemsSource = cartItems;
            EmptyCartHint.Visibility = cartItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            LblCartCount.Text = pozycji + (pozycji == 1 ? " pozycja" : pozycji > 1 && pozycji < 5 ? " pozycje" : " pozycji");
            Step3Sub.Text = pozycji == 0 ? "Wybierz produkty" : $"{pozycji} poz · {kg:N0} kg";

            UpdateLimitDisplay();
            UpdateValidation();
        }

        private static string BuildTags(ProductVm p)
        {
            var t = new List<string>();
            if (p.E2) t.Add("E2");
            if (p.Folia) t.Add("Folia");
            if (p.Hallal) t.Add("Hallal");
            return t.Count == 0 ? "" : "🏷 " + string.Join(" · ", t);
        }

        private void UpdateLimitDisplay()
        {
            double headerBarMax = HdrProgressLimit.Parent is Border parentBar && parentBar.ActualWidth > 0
                ? parentBar.ActualWidth : 280;
            if (_wybranyKlient == null || _wybranyKlient.LimitKredytowy <= 0)
            {
                HdrLimitPctLabel.Text = "—";
                HdrProgressLimit.Width = 0;
                HdrLblLimitHint.Text = _wybranyKlient == null ? "Wybierz klienta" : "Brak limitu";
                ChipLimit.Text = "—";
                return;
            }

            decimal wykorzystany = _wybranyKlient.DoZaplacenia;
            decimal limit = _wybranyKlient.LimitKredytowy;
            decimal pct = Math.Min(100, (wykorzystany / limit) * 100);
            HdrLimitPctLabel.Text = pct.ToString("N0", _pl) + "%";
            HdrProgressLimit.Width = Math.Max(0, headerBarMax * (double)(pct / 100m));
            HdrLblLimitHint.Text = $"{wykorzystany:N0}/{limit:N0} zł · pozostało {(limit - wykorzystany):N0} zł";
            ChipLimit.Text = pct.ToString("N0", _pl) + "%";

            if (pct >= 100)
            {
                HdrProgressLimit.Background = (Brush)FindResource("Danger");
                HdrLblLimitHint.Foreground = (Brush)FindResource("Danger");
            }
            else if (pct > 80)
            {
                HdrProgressLimit.Background = (Brush)FindResource("Warning");
                HdrLblLimitHint.Foreground = (Brush)FindResource("TextSecondary");
            }
            else
            {
                HdrProgressLimit.Background = (Brush)FindResource("Success");
                HdrLblLimitHint.Foreground = (Brush)FindResource("TextSecondary");
            }
        }

        private void UpdateValidation()
        {
            bool hasClient = _wybranyKlient != null;
            bool hasTerm = _wybranaData.Date >= DateTime.Today;
            bool hasItems = _produkty.Any(p => p.QtyKg > 0);

            SetValIndicator(ValKlient, ValKlientChip, hasClient);
            SetValIndicator(ValTermin, ValTerminChip, hasTerm);
            SetValIndicator(ValPozycje, ValPozycjeChip, hasItems);

            BtnSave.IsEnabled = hasClient && hasTerm && hasItems;
        }

        private static readonly Brush _validChipBg = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7")!);
        private void SetValIndicator(TextBlock tb, Border chip, bool ok)
        {
            tb.Text = ok ? "✓" : "○";
            tb.Foreground = ok ? (Brush)FindResource("Success") : (Brush)FindResource("TextMuted");
            chip.Background = ok ? _validChipBg : Brushes.Transparent;
        }

        // ════════════════════ ZAPIS ════════════════════

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_wybranyKlient == null) return;
            var inCart = _produkty.Where(p => p.QtyKg > 0).ToList();
            if (inCart.Count == 0) return;

            decimal totalPalety = inCart.Sum(p => p.Palety);
            if (totalPalety > LIMIT_PALET_TIR)
            {
                var r = MessageBox.Show(this,
                    $"Łączna liczba palet ({totalPalety:N1}) przekracza limit TIR ({LIMIT_PALET_TIR}).\n\nKontynuować zapis?",
                    "Przekroczenie limitu", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (r != MessageBoxResult.Yes) return;
            }

            BtnSave.IsEnabled = false;
            Cursor = Cursors.Wait;
            try
            {
                int orderId = await SaveOrderAsync(inCart);
                ShowToast($"✓ Zamówienie #{orderId} zapisane", true);
                await Task.Delay(900);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd zapisu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BtnSave.IsEnabled = true;
                Cursor = Cursors.Arrow;
            }
        }

        private async Task<int> SaveOrderAsync(List<ProductVm> items)
        {
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            bool dataProdExists = await ColumnExistsAsync(cn, "ZamowieniaMieso", "DataProdukcji");
            bool dataUbojuExists = await ColumnExistsAsync(cn, "ZamowieniaMieso", "DataUboju");
            bool strefaTowarExists = await ColumnExistsAsync(cn, "ZamowieniaMiesoTowar", "Strefa");

            await using var tr = (SqlTransaction)await cn.BeginTransactionAsync();

            decimal sumaPoj = items.Sum(p => p.Pojemniki);
            decimal sumaPal = items.Sum(p => p.Palety);
            bool czyE2 = items.Any(p => p.E2);
            string transportStatus = ChkWlasnyOdbior.IsChecked == true ? "Wlasny" : "Oczekuje";

            DateTime dataProdukcji = _dataProdukcji;
            DateTime dataPrzyjazdu = _wybranaData.Date.Add(_wybranaGodzina);

            var cmdGetId = new SqlCommand("SELECT ISNULL(MAX(Id), 0) + 1 FROM dbo.ZamowieniaMieso", cn, tr);
            int orderId = Convert.ToInt32(await cmdGetId.ExecuteScalarAsync());

            string insertCols = "Id, DataZamowienia, DataPrzyjazdu, KlientId, Uwagi, IdUser, DataUtworzenia, LiczbaPojemnikow, LiczbaPalet, TrybE2, TransportStatus";
            string insertVals = "@id, @dz, @dp, @kid, @uw, @u, GETDATE(), @poj, @pal, @e2, @ts";
            if (dataProdExists) { insertCols += ", DataProdukcji"; insertVals += ", @dprod"; }
            if (dataUbojuExists) { insertCols += ", DataUboju"; insertVals += ", @duboj"; }

            var cmdIns = new SqlCommand($"INSERT INTO dbo.ZamowieniaMieso ({insertCols}) VALUES ({insertVals})", cn, tr);
            cmdIns.Parameters.AddWithValue("@id", orderId);
            cmdIns.Parameters.AddWithValue("@dz", _wybranaData.Date);
            cmdIns.Parameters.AddWithValue("@dp", dataPrzyjazdu);
            cmdIns.Parameters.AddWithValue("@kid", int.Parse(_wybranyKlient!.Id));
            cmdIns.Parameters.AddWithValue("@uw", string.IsNullOrWhiteSpace(TxtUwagi.Text) ? (object)DBNull.Value : TxtUwagi.Text);
            cmdIns.Parameters.AddWithValue("@u", UserID);
            cmdIns.Parameters.AddWithValue("@poj", (int)Math.Round(sumaPoj));
            cmdIns.Parameters.AddWithValue("@pal", sumaPal);
            cmdIns.Parameters.AddWithValue("@e2", czyE2);
            cmdIns.Parameters.AddWithValue("@ts", transportStatus);
            if (dataProdExists) cmdIns.Parameters.AddWithValue("@dprod", dataProdukcji);
            if (dataUbojuExists) cmdIns.Parameters.AddWithValue("@duboj", dataProdukcji);
            await cmdIns.ExecuteNonQueryAsync();

            string strefaCol = strefaTowarExists ? ", Strefa" : "";
            string strefaVal = strefaTowarExists ? ", @strefa" : "";
            var cmdItem = new SqlCommand(
                $@"INSERT INTO dbo.ZamowieniaMiesoTowar
                   (ZamowienieId, KodTowaru, Ilosc, Cena, Pojemniki, Palety, E2, Folia, Hallal{strefaCol})
                   VALUES (@zid, @kt, @il, @ce, @poj, @pal, @e2, @folia, @hallal{strefaVal})", cn, tr);

            cmdItem.Parameters.Add("@zid", SqlDbType.Int);
            cmdItem.Parameters.Add("@kt", SqlDbType.Int);
            cmdItem.Parameters.Add("@il", SqlDbType.Decimal);
            cmdItem.Parameters.Add("@ce", SqlDbType.VarChar, 20);
            cmdItem.Parameters.Add("@poj", SqlDbType.Int);
            cmdItem.Parameters.Add("@pal", SqlDbType.Decimal);
            cmdItem.Parameters.Add("@e2", SqlDbType.Bit);
            cmdItem.Parameters.Add("@folia", SqlDbType.Bit);
            cmdItem.Parameters.Add("@hallal", SqlDbType.Bit);
            if (strefaTowarExists) cmdItem.Parameters.Add("@strefa", SqlDbType.Bit);

            foreach (var p in items)
            {
                cmdItem.Parameters["@zid"].Value = orderId;
                cmdItem.Parameters["@kt"].Value = p.Id;
                cmdItem.Parameters["@il"].Value = p.QtyKg;

                string cenaOut = "0";
                if (!string.IsNullOrWhiteSpace(p.Cena) && decimal.TryParse(p.Cena.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var ceVal) && ceVal > 0)
                    cenaOut = ceVal.ToString("F2", CultureInfo.InvariantCulture);
                cmdItem.Parameters["@ce"].Value = cenaOut;

                cmdItem.Parameters["@poj"].Value = (int)Math.Round(p.Pojemniki);
                cmdItem.Parameters["@pal"].Value = p.Palety;
                cmdItem.Parameters["@e2"].Value = p.E2;
                cmdItem.Parameters["@folia"].Value = p.Folia;
                cmdItem.Parameters["@hallal"].Value = p.Hallal;
                if (strefaTowarExists) cmdItem.Parameters["@strefa"].Value = p.Strefa;

                await cmdItem.ExecuteNonQueryAsync();
            }

            await tr.CommitAsync();
            return orderId;
        }

        private async Task<bool> ColumnExistsAsync(SqlConnection cn, string table, string column)
        {
            var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(@t) AND name = @c", cn);
            cmd.Parameters.AddWithValue("@t", "dbo." + table);
            cmd.Parameters.AddWithValue("@c", column);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
        }

        // ════════════════════ ACTION BAR ════════════════════

        private void BtnCancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
        private void BtnClose_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        private void TxtUwagi_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtUwagiPlaceholder.Visibility = string.IsNullOrEmpty(TxtUwagi.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ════════════════════ TOAST ════════════════════

        private DispatcherTimer? _toastTimer;
        private void ShowToast(string text, bool success)
        {
            ToastText.Text = text;
            ToastIcon.Text = success ? "✓" : "⚠";
            ToastIcon.Foreground = success ? (Brush)FindResource("Success") : (Brush)FindResource("Warning");
            Toast.Visibility = Visibility.Visible;
            Toast.Opacity = 0;

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
            Toast.BeginAnimation(OpacityProperty, fadeIn);

            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer!.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                fadeOut.Completed += (_, _) => Toast.Visibility = Visibility.Collapsed;
                Toast.BeginAnimation(OpacityProperty, fadeOut);
            };
            _toastTimer.Start();
        }

        // ═══════════════════════════════════════════════════════════
        // VIEW MODELS
        // ═══════════════════════════════════════════════════════════

        public class KontrahentVm : INotifyPropertyChanged
        {
            public string Id { get; set; } = "";
            public string Nazwa { get; set; } = "";
            public string NIP { get; set; } = "";
            public string KodPocztowy { get; set; } = "";
            public string Miejscowosc { get; set; } = "";
            public string Handlowiec { get; set; } = "";
            public DateTime? OstatnieZamowienie { get; set; }
            public decimal LimitKredytowy { get; set; }
            public decimal DoZaplacenia { get; set; }

            public string LastOrderDisplay { get; set; } = "";
            public string LastOrderShort { get; set; } = "";
            public string HandlowiecShort { get; set; } = "";
            public string NipDisplay { get; set; } = "";
            public string Initials { get; set; } = "";
            public Brush AvatarBrush { get; set; } = Brushes.Gray;
            public string LimitBadge { get; set; } = "";
            public Brush LimitBadgeBg { get; set; } = Brushes.LightGray;
            public Brush LimitBadgeFg { get; set; } = Brushes.Gray;

            // Preferencje klienta wyliczone z historii zamówień
            public int? PreferredHour { get; set; }
            public int? PreferredDeliveryDiff { get; set; }

#pragma warning disable CS0067
            public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
        }

        public class DayVm
        {
            public DateTime Date { get; set; }
            public string DayName { get; set; } = "";
            public string DayNum { get; set; } = "";
            public string MonthShort { get; set; } = "";
            public string LoadDisplay { get; set; } = "";
            public bool IsSelected { get; set; }
            public Brush BgBrush { get; set; } = Brushes.White;
            public Brush BorderBrush { get; set; } = Brushes.LightGray;
            public Brush ForeBrush { get; set; } = Brushes.Black;
            public Brush LoadBrush { get; set; } = Brushes.Gray;
            public double LoadBarWidth { get; set; }
        }

        public class HourVm
        {
            public TimeSpan Hour { get; set; }
            public string HourDisplay { get; set; } = "";
            public Brush BgBrush { get; set; } = Brushes.White;
            public Brush BorderBrush { get; set; } = Brushes.LightGray;
            public Brush ForeBrush { get; set; } = Brushes.Black;
        }

        public class ProductVm : INotifyPropertyChanged
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
            public string Katalog { get; set; } = "";
            public string KategoriaDisplay { get; set; } = "";
            public decimal QtyKg { get; set; }
            public bool E2 { get; set; }
            public bool Folia { get; set; }
            public bool Hallal { get; set; }
            public bool Strefa { get; set; }
            public string? Cena { get; set; }

            public decimal Pojemniki { get; set; }
            public decimal Palety { get; set; }

            public string QtyKgDisplay { get; set; } = "0";
            public string PojDisplay { get; set; } = "0";
            public string PalDisplay { get; set; } = "0";
            public string CenaDisplay { get; set; } = "";
            public Visibility InCartVisibility { get; set; } = Visibility.Collapsed;
            public string InCartBadge { get; set; } = "";
            public Brush ProductBorder { get; set; } = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EAEDF1")!);
            public Thickness ProductBorderThickness { get; set; } = new Thickness(1);
            public ImageSource? ImageSource { get; set; }
            public Visibility HasImageVisibility { get; set; } = Visibility.Collapsed;
            public Visibility PlaceholderVisibility { get; set; } = Visibility.Visible;
            public string PlaceholderEmoji { get; set; } = "🍗";

            public event PropertyChangedEventHandler? PropertyChanged;
            public void NotifyAll(string? skip = null)
            {
                if (skip != "kg")  PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QtyKgDisplay)));
                if (skip != "poj") PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PojDisplay)));
                if (skip != "pal") PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PalDisplay)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CenaDisplay)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InCartVisibility)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InCartBadge)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(E2)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Folia)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Hallal)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Strefa)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProductBorder)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProductBorderThickness)));
            }
        }

        public class CartItem
        {
            public int Id { get; set; }
            public string Kod { get; set; } = "";
            public string Detail { get; set; } = "";
            public string CenaDisplay { get; set; } = "";
            public string WartoscDisplay { get; set; } = "";
            public Visibility CenaVisibility { get; set; } = Visibility.Collapsed;
            public ImageSource? ImageSource { get; set; }
            public Visibility HasImageVisibility { get; set; } = Visibility.Collapsed;
            public Visibility PlaceholderVisibility { get; set; } = Visibility.Visible;
            public string PlaceholderEmoji { get; set; } = "🍗";
            public string IconE2 { get; set; } = "";
            public Visibility IconE2Visibility { get; set; } = Visibility.Collapsed;
            public string IconFolia { get; set; } = "";
            public Visibility IconFoliaVisibility { get; set; } = Visibility.Collapsed;
            public string IconHallal { get; set; } = "";
            public Visibility IconHallalVisibility { get; set; } = Visibility.Collapsed;
            public string IconStrefa { get; set; } = "";
            public Visibility IconStrefaVisibility { get; set; } = Visibility.Collapsed;
        }
    }
}
