using Microsoft.Data.SqlClient;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Kalendarz1.Services
{
    public partial class PowiadomieniaZamowienPopup : Window
    {
        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private readonly PowiadomieniaZamowienService.PowiadomienieRekord _rec;
        private readonly System.Collections.Generic.List<PowiadomieniaZamowienService.PowiadomienieRekord>? _wszystkieZmiany;
        private DispatcherTimer? _autoCloseTimer;

        public class ZmianaWiersz
        {
            public string Ikona { get; set; } = "";
            public string Opis { get; set; } = "";
            public string Kg { get; set; } = "";
            public Brush Kolor { get; set; } = Brushes.Gray;
        }

        // Static cache mapowania HandlowiecName → UserID + UserID → Name (operators) + avatarów (shared między popupami)
        private static readonly ConcurrentDictionary<string, string> _staticHandlowiecMap = new(StringComparer.OrdinalIgnoreCase);
        private static volatile bool _staticHandlowiecMapLoaded;
        private static readonly ConcurrentDictionary<string, string> _staticUserIdToNameMap = new(StringComparer.OrdinalIgnoreCase);
        private static volatile bool _staticUserMapLoaded;
        private static readonly ConcurrentDictionary<string, BitmapSource> _staticAvatarCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly CultureInfo Pl = new("pl-PL");

        public PowiadomieniaZamowienPopup(PowiadomieniaZamowienService.PowiadomienieRekord r)
        {
            InitializeComponent();
            _rec = r;
            Render();
            PositionInBottomRight();
            _ = LoadAssetsAsync();

            _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _autoCloseTimer.Tick += (s, e) => Close();
            _autoCloseTimer.Start();
        }

        // Konstruktor zbiorczy — wiele zmian od tego samego usera dla tego samego zamówienia w krótkim czasie.
        // Pierwsza zmiana to "wiodąca" (avatar, klient, handlowiec, daty), reszta agregowana w listę.
        public PowiadomieniaZamowienPopup(System.Collections.Generic.List<PowiadomieniaZamowienService.PowiadomienieRekord> records)
        {
            InitializeComponent();
            _rec = records[0];
            _wszystkieZmiany = records;
            Render();
            RenderMultiList();
            PositionInBottomRight();
            _ = LoadAssetsAsync();

            // Auto-close dłuższy dla multi (45s — więcej tekstu do przeczytania)
            _autoCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(records.Count > 1 ? 45 : 30) };
            _autoCloseTimer.Tick += (s, e) => Close();
            _autoCloseTimer.Start();
        }

        // Renderuj listę zmian w trybie multi (zbiorczy popup)
        private void RenderMultiList()
        {
            if (_wszystkieZmiany == null || _wszystkieZmiany.Count <= 1) return;

            // Header: "Wiele zmian po cut-off"
            LblHeaderSub.Text = $"{_wszystkieZmiany.Count} zmian w zamówieniu #{_rec.ZamowienieId}";

            // Akcja: pokaż "Edytuje wielokrotnie" zamiast pojedynczej akcji
            LblAkcjaIkona.Text = "🔁";
            LblAkcja.Text = $"{_wszystkieZmiany.Count} zmian";
            LblTowar.Text = $"zamówienie #{_rec.ZamowienieId} — {_rec.KlientNazwa}";

            // ZmianaBox: suma wszystkich zmian
            decimal sumaPlus = 0, sumaMinus = 0;
            foreach (var r in _wszystkieZmiany)
            {
                if (r.Akcja == PowiadomieniaZamowienService.AkcjaDodanie
                    || r.Akcja == PowiadomieniaZamowienService.AkcjaZwiekszenie)
                    sumaPlus += r.ZmianaKg;
                else if (r.Akcja == PowiadomieniaZamowienService.AkcjaUsuniecie
                    || r.Akcja == PowiadomieniaZamowienService.AkcjaZmniejszenie)
                    sumaMinus += r.ZmianaKg;
            }
            decimal netto = sumaPlus - sumaMinus;
            LblZmianaTytul.Text = "Łączna zmiana";
            LblStaraNowa.Text = $"+{sumaPlus:N0} kg / −{sumaMinus:N0} kg";
            LblZmianaKg.Text = (netto >= 0 ? "+" : "−") + $"{Math.Abs(netto):N0} kg";

            // Lista mini-wierszy
            var wiersze = new System.Collections.Generic.List<ZmianaWiersz>();
            foreach (var r in _wszystkieZmiany)
            {
                (string ikona, Brush kolor) = r.Akcja switch
                {
                    PowiadomieniaZamowienService.AkcjaDodanie => ("➕", (Brush)new SolidColorBrush(Color.FromRgb(0x15, 0x80, 0x3D))),
                    PowiadomieniaZamowienService.AkcjaZwiekszenie => ("📈", new SolidColorBrush(Color.FromRgb(0x92, 0x40, 0x0E))),
                    PowiadomieniaZamowienService.AkcjaZmniejszenie => ("📉", new SolidColorBrush(Color.FromRgb(0x1E, 0x40, 0xAF))),
                    PowiadomieniaZamowienService.AkcjaUsuniecie => ("🗑", new SolidColorBrush(Color.FromRgb(0x99, 0x1B, 0x1B))),
                    _ => ("ℹ️", (Brush)Brushes.Gray)
                };
                string opis = string.IsNullOrWhiteSpace(r.NazwaTowaru) ? "(całe zamówienie)" : r.NazwaTowaru;
                string kg = r.Akcja == PowiadomieniaZamowienService.AkcjaDodanie
                    ? $"+{r.NowaIlosc:N0} kg"
                    : r.Akcja == PowiadomieniaZamowienService.AkcjaUsuniecie
                        ? $"−{r.StaraIlosc:N0} kg"
                        : (r.NowaIlosc - r.StaraIlosc >= 0 ? "+" : "−") + $"{r.ZmianaKg:N0} kg";
                wiersze.Add(new ZmianaWiersz { Ikona = ikona, Opis = opis, Kg = kg, Kolor = kolor });
            }
            ListaZmian.ItemsSource = wiersze;
            MultiListBox.Visibility = Visibility.Visible;
        }

        private void Render()
        {
            LblHeaderSub.Text = _rec.Typ == "NOWE" ? "Nowe zamówienie po godzinie cut-off" : "Edycja zamówienia po godzinie cut-off";
            LblKlient.Text = string.IsNullOrWhiteSpace(_rec.KlientNazwa) ? "(brak nazwy)" : _rec.KlientNazwa;
            LblZamId.Text = $"#{_rec.ZamowienieId}";
            LblHandlowiec.Text = string.IsNullOrWhiteSpace(_rec.Handlowiec) ? _rec.UtworzonoPrzez : _rec.Handlowiec;
            LblHandlowiecInicjal.Text = GetInicjaly(LblHandlowiec.Text);
            LblCzas.Text = FormatTimeAgo(_rec.UtworzonoAt);

            // Autor zmiany (kto kliknął "Zapisz" w nowym/edycyjnym oknie)
            LblZmienilAkcja.Text = _rec.Typ == "NOWE" ? "➕ Dodał:" : "✏️ Zmienił:";
            LblAutor.Text = string.IsNullOrWhiteSpace(_rec.UtworzonoPrzez) ? "—" : _rec.UtworzonoPrzez; // UserID — zostanie podmienione na display name po LoadAssetsAsync
            LblAutorInicjal.Text = GetInicjaly(LblAutor.Text);

            // Dzień uboju: "Pt 23.05.2026" + relatywne "(dzisiaj)" / "(jutro)" / "(za 3 dni)" / "(2 dni temu)"
            if (_rec.DataUboju.HasValue)
            {
                var d = _rec.DataUboju.Value.Date;
                string dzienTyg = SkrotDniaTyg(d.DayOfWeek);
                LblDataUboju.Text = $"{dzienTyg} {d:dd.MM.yyyy}";
                LblDataUbojuRelatywne.Text = FormatRelatywne(d);
                LblDataUbojuRelatywne.Foreground = KolorRelatywne(d);
            }
            else
            {
                LblDataUboju.Text = "—";
                LblDataUbojuRelatywne.Text = "";
            }

            (string ikona, string opisAkcji, string kolorZmianyBg, string kolorZmianyFg, string accentColor) = _rec.Akcja switch
            {
                PowiadomieniaZamowienService.AkcjaDodanie       => ("➕", "Dodanie pozycji",      "#DCFCE7", "#15803D", "#16A34A"),  // zielony
                PowiadomieniaZamowienService.AkcjaZwiekszenie   => ("📈", "Zwiększenie ilości",   "#FEF3C7", "#92400E", "#F59E0B"),  // pomarańczowy
                PowiadomieniaZamowienService.AkcjaZmniejszenie  => ("📉", "Zmniejszenie ilości",  "#DBEAFE", "#1E40AF", "#3B82F6"),  // niebieski
                PowiadomieniaZamowienService.AkcjaUsuniecie     => ("🗑", "Usunięcie pozycji",    "#FEE2E2", "#991B1B", "#DC2626"),  // czerwony
                _                                               => ("ℹ️", _rec.Akcja,             "#F1F5F9", "#475569", "#6B7280")
            };

            // Side accent stripe + ProgressFill — kolor zależny od typu akcji
            try
            {
                var accentBrush = (Brush)new BrushConverter().ConvertFromString(accentColor)!;
                accentBrush.Freeze();
                SideAccentStripe.Background = accentBrush;
                ProgressFill.Background = accentBrush;
            }
            catch { }

            LblAkcjaIkona.Text = ikona;
            LblAkcja.Text = opisAkcji;
            LblTowar.Text = string.IsNullOrWhiteSpace(_rec.NazwaTowaru) ? "(całe zamówienie)" : _rec.NazwaTowaru;

            string staraNowa;
            if (_rec.Akcja == PowiadomieniaZamowienService.AkcjaDodanie)
                staraNowa = $"0 → {_rec.NowaIlosc:N0} kg";
            else if (_rec.Akcja == PowiadomieniaZamowienService.AkcjaUsuniecie)
                staraNowa = $"{_rec.StaraIlosc:N0} → 0 kg";
            else
                staraNowa = $"{_rec.StaraIlosc:N0} → {_rec.NowaIlosc:N0} kg";
            LblStaraNowa.Text = staraNowa;
            LblZmianaKg.Text = $"{_rec.ZmianaKg:N0} kg";

            try
            {
                var bg = (Brush)new BrushConverter().ConvertFromString(kolorZmianyBg)!;
                var fg = (Brush)new BrushConverter().ConvertFromString(kolorZmianyFg)!;
                ZmianaBox.Background = bg;
                LblZmianaKg.Foreground = fg;
            }
            catch { }
        }

        // ── Asset loading: zdjęcie towaru + avatar handlowca (background) ──
        private async Task LoadAssetsAsync()
        {
            try
            {
                // Zdjęcie towaru z TowaryZdjeciaService (cached BLOB)
                if (_rec.KodTowaru.HasValue)
                {
                    await Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.LoadAsync(ConnLibra);
                    var img = Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.Get(_rec.KodTowaru.Value);
                    if (img != null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ImgTowar.Source = img;
                            ImgTowar.Visibility = Visibility.Visible;
                            LblTowarEmoji.Visibility = Visibility.Collapsed;
                        });
                    }
                }

                // Avatar handlowca przez UserHandlowcy → UserID → UserAvatarManager
                string handlowiec = string.IsNullOrWhiteSpace(_rec.Handlowiec) ? _rec.UtworzonoPrzez : _rec.Handlowiec;
                if (!string.IsNullOrWhiteSpace(handlowiec))
                {
                    var avatar = await GetAvatarForHandlowiecAsync(handlowiec);
                    if (avatar != null)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            ImgHandlowiecAvatar.Source = avatar;
                            ImgHandlowiecAvatar.Visibility = Visibility.Visible;
                            LblHandlowiecInicjal.Visibility = Visibility.Collapsed;
                        });
                    }
                }

                // Autor zmiany (UtworzonoPrzez = UserID) — pobierz display name z operators + avatar
                if (!string.IsNullOrWhiteSpace(_rec.UtworzonoPrzez))
                {
                    await EnsureUserMapLoadedAsync();
                    string displayName = _staticUserIdToNameMap.TryGetValue(_rec.UtworzonoPrzez, out var n) && !string.IsNullOrWhiteSpace(n)
                        ? n
                        : _rec.UtworzonoPrzez;

                    var avatarAutor = await GetAvatarByUserIdAsync(_rec.UtworzonoPrzez, displayName);
                    Dispatcher.Invoke(() =>
                    {
                        LblAutor.Text = displayName;
                        LblAutorInicjal.Text = GetInicjaly(displayName);
                        if (avatarAutor != null)
                        {
                            ImgAutorAvatar.Source = avatarAutor;
                            ImgAutorAvatar.Visibility = Visibility.Visible;
                            LblAutorInicjal.Visibility = Visibility.Collapsed;
                        }
                    });
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Popup assets] {ex.Message}"); }
        }

        // Pobiera avatar BEZPOŚREDNIO po UserID (bez mapowania HandlowiecName) — dla autora zmiany.
        private static async Task<BitmapSource?> GetAvatarByUserIdAsync(string userId, string displayName)
        {
            string cacheKey = "uid:" + userId;
            if (_staticAvatarCache.TryGetValue(cacheKey, out var cached)) return cached;

            BitmapSource? bmp = null;
            try
            {
                if (Kalendarz1.UserAvatarManager.HasAvatar(userId))
                {
                    using var av = Kalendarz1.UserAvatarManager.GetAvatarRounded(userId, 48);
                    if (av != null) bmp = ConvertToBitmapSource(av);
                }
                if (bmp == null)
                {
                    using var defAv = Kalendarz1.UserAvatarManager.GenerateDefaultAvatar(displayName, userId, 48);
                    if (defAv != null) bmp = ConvertToBitmapSource(defAv);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Avatar autor] {ex.Message}"); }
            await Task.CompletedTask;

            if (bmp != null)
            {
                bmp.Freeze();
                _staticAvatarCache[cacheKey] = bmp;
            }
            return bmp;
        }

        // Pobiera mapowanie UserID → display name (z LibraNet.dbo.operators) — raz na proces.
        private static async Task EnsureUserMapLoadedAsync()
        {
            if (_staticUserMapLoaded) return;
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT ID, Name FROM dbo.operators", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var id = rd.IsDBNull(0) ? "" : rd.GetString(0);
                    var name = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    if (!string.IsNullOrWhiteSpace(id)) _staticUserIdToNameMap[id] = name;
                }
                _staticUserMapLoaded = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[UserMap] {ex.Message}"); }
        }

        private static async Task<BitmapSource?> GetAvatarForHandlowiecAsync(string handlowiec)
        {
            if (_staticAvatarCache.TryGetValue(handlowiec, out var cached)) return cached;

            await EnsureHandlowiecMapLoadedAsync();

            BitmapSource? bmp = null;
            try
            {
                _staticHandlowiecMap.TryGetValue(handlowiec, out var uid);
                if (!string.IsNullOrEmpty(uid) && Kalendarz1.UserAvatarManager.HasAvatar(uid))
                {
                    using var av = Kalendarz1.UserAvatarManager.GetAvatarRounded(uid, 48);
                    if (av != null) bmp = ConvertToBitmapSource(av);
                }
                if (bmp == null)
                {
                    using var defAv = Kalendarz1.UserAvatarManager.GenerateDefaultAvatar(handlowiec, uid ?? handlowiec, 48);
                    if (defAv != null) bmp = ConvertToBitmapSource(defAv);
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Avatar handlowca] {ex.Message}"); }

            if (bmp != null)
            {
                bmp.Freeze();
                _staticAvatarCache[handlowiec] = bmp;
            }
            return bmp;
        }

        private static async Task EnsureHandlowiecMapLoadedAsync()
        {
            if (_staticHandlowiecMapLoaded) return;
            try
            {
                await using var cn = new SqlConnection(ConnLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    var name = rd.IsDBNull(0) ? "" : rd.GetString(0);
                    var uid = rd.IsDBNull(1) ? "" : rd.GetString(1);
                    if (!string.IsNullOrWhiteSpace(name)) _staticHandlowiecMap[name] = uid;
                }
                _staticHandlowiecMapLoaded = true;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HandlowiecMap] {ex.Message}"); }
        }

        // ── Helpers ────────────────────────────────────────────────────────
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private static BitmapSource? ConvertToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;
            using var bmp = new System.Drawing.Bitmap(image);
            var hBmp = bmp.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally { DeleteObject(hBmp); }
        }

        private static string SkrotDniaTyg(DayOfWeek d) => d switch
        {
            DayOfWeek.Monday    => "Pn",
            DayOfWeek.Tuesday   => "Wt",
            DayOfWeek.Wednesday => "Śr",
            DayOfWeek.Thursday  => "Cz",
            DayOfWeek.Friday    => "Pt",
            DayOfWeek.Saturday  => "Sob",
            DayOfWeek.Sunday    => "Nd",
            _ => ""
        };

        private static string FormatRelatywne(DateTime data)
        {
            int diff = (int)(data.Date - DateTime.Today).TotalDays;
            return diff switch
            {
                0 => "(dzisiaj)",
                1 => "(jutro)",
                -1 => "(wczoraj)",
                > 1 => $"(za {diff} dni)",
                < -1 => $"({-diff} dni temu)"
            };
        }

        private static Brush KolorRelatywne(DateTime data)
        {
            int diff = (int)(data.Date - DateTime.Today).TotalDays;
            if (diff == 0) return new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));  // czerwony — dzisiaj!
            if (diff == 1) return new SolidColorBrush(Color.FromRgb(0xEA, 0x58, 0x0C));  // pomarańczowy — jutro
            if (diff > 1)  return new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));  // zielony — za N dni (OK, planowane)
            return new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));                 // szary — wczoraj/przeszłość
        }

        private static string GetInicjaly(string? imie)
        {
            if (string.IsNullOrWhiteSpace(imie)) return "?";
            var parts = imie.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0][0].ToString().ToUpper();
            return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
        }

        private void PositionInBottomRight()
        {
            try
            {
                var workArea = SystemParameters.WorkArea;
                this.SourceInitialized += (s, e) =>
                {
                    Left = workArea.Right - this.ActualWidth - 16;
                    Top = workArea.Bottom - this.ActualHeight - 16;
                };
                this.ContentRendered += (s, e) =>
                {
                    double targetLeft = workArea.Right - this.ActualWidth - 16;
                    double targetTop = workArea.Bottom - this.ActualHeight - 16;
                    Top = targetTop;

                    // Slide-in z prawej krawędzi ekranu — 220ms CubicEase OUT
                    Left = workArea.Right;
                    var anim = new System.Windows.Media.Animation.DoubleAnimation
                    {
                        From = workArea.Right,
                        To = targetLeft,
                        Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                        EasingFunction = new System.Windows.Media.Animation.CubicEase
                        {
                            EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                        }
                    };
                    this.BeginAnimation(LeftProperty, anim);

                    // Start animacji ProgressFill: szerokość 0 → ActualWidth (po 1 frame'ie ActualWidth jest znane)
                    StartProgressBarAnimation();
                };
            }
            catch { }
        }

        private void StartProgressBarAnimation()
        {
            try
            {
                double durationSeconds = (_wszystkieZmiany != null && _wszystkieZmiany.Count > 1) ? 45 : 30;
                double maxWidth = ProgressFill.Parent is FrameworkElement parent ? parent.ActualWidth : 400;

                // Start: szerokość = max. End: szerokość = 0 (topnieje)
                var anim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    From = maxWidth,
                    To = 0,
                    Duration = new Duration(TimeSpan.FromSeconds(durationSeconds)),
                    EasingFunction = new System.Windows.Media.Animation.QuadraticEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseIn
                    }
                };
                ProgressFill.BeginAnimation(System.Windows.FrameworkElement.WidthProperty, anim);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ProgressBar] {ex.Message}"); }
        }

        private static string FormatTimeAgo(DateTime at)
        {
            var diff = DateTime.Now - at;
            if (diff.TotalSeconds < 60) return "przed chwilą";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes} min temu";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours} h temu";
            return at.ToString("dd.MM HH:mm");
        }

        private void BtnDismiss_Click(object sender, RoutedEventArgs e) => Close();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            _autoCloseTimer?.Stop();
            _autoCloseTimer = null;
            base.OnClosed(e);
        }
    }
}
