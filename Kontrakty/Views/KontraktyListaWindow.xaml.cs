using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Kalendarz1.Kontrakty.Models;
using Kalendarz1.Kontrakty.Services;

namespace Kalendarz1.Kontrakty.Views
{
    /// <summary>
    /// Główne okno modułu Kontrakty Hodowców — lista + filtry + statystyki + avatary twórców.
    /// </summary>
    public partial class KontraktyListaWindow : Window
    {
        private readonly KontraktyService _service = new();
        private readonly DispatcherTimer _debounce;
        private readonly Dictionary<string, BitmapSource> _avatarCache = new();
        private bool _ready;

        public KontraktyListaWindow()
        {
            InitializeComponent();
            _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounce.Tick += async (_, _) => { _debounce.Stop(); await ZaladujListeAsync(); };
            Loaded += KontraktyListaWindow_Loaded;
        }

        private async void KontraktyListaWindow_Loaded(object sender, RoutedEventArgs e)
        {
            bool schemat = await _service.CzySchematIstniejeAsync();
            banerSchema.Visibility = schemat ? Visibility.Collapsed : Visibility.Visible;
            _ready = true;
            if (schemat) _ = _service.GenerujAlertyAsync(); // idempotentne — wypełnia KontraktyAlerty
            await ZaladujListeAsync();
            await ZaladujInwentarzAsync();
        }

        // ── Ładowanie listy ──────────────────────────────────────────────────
        private async System.Threading.Tasks.Task ZaladujListeAsync()
        {
            try
            {
                string status = TagOf(cbStatus) ?? "AKTYWNE";
                string typ = TagOf(cbTyp) ?? "WSZYSTKIE";
                string? moje = chkMoje.IsChecked == true ? (Kalendarz1.App.UserID ?? "") : null;
                var dane = await _service.GetKontraktyAsync(txtSzukaj.Text, status, typ, chkArimr.IsChecked == true, moje);
                dgKontrakty.ItemsSource = dane;
                txtLicznik.Text = $"📑 {dane.Count} " + Odmiana(dane.Count, "kontrakt", "kontrakty", "kontraktów");
                txtPusto.Visibility = dane.Count == 0 && banerSchema.Visibility != Visibility.Visible
                    ? Visibility.Visible : Visibility.Collapsed;
                _ = LadujAvataryAsync(dane);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd ładowania listy: " + ex.Message, "Kontrakty",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async System.Threading.Tasks.Task ZaladujInwentarzAsync()
        {
            var inw = await _service.GetInwentarzAsync();
            chipRazem.Text = $"Razem: {inw.Razem}";
            chipAktywne.Text = $"● Aktywne: {inw.Aktywne}";
            chipWygasajace.Text = $"⏰ Wygasają ≤90: {inw.Wygasajace90}";
            chipWygasle.Text = $"✕ Wygasłe: {inw.Wygasle}";
            chipRobocze.Text = $"◌ Robocze: {inw.Robocze}";

            if (inw.Wygasajace90 > 0)
            {
                alertBadge.Visibility = Visibility.Visible;
                txtAlertBadge.Text = $"⏰ {inw.Wygasajace90} do działania";
            }
            else alertBadge.Visibility = Visibility.Collapsed;
        }

        // ── Avatary twórców (progresywnie w tle, jak w starym programie) ──────
        private async System.Threading.Tasks.Task LadujAvataryAsync(List<KontraktListItem> items)
        {
            int i = 0;
            foreach (var it in items)
            {
                if (it.AvatarUtw == null && !string.IsNullOrWhiteSpace(it.UtworzylUserId))
                {
                    var av = PobierzAvatar(it.UtworzylUserId, it.UtworzylNazwa);
                    if (av != null) it.AvatarUtw = av;
                }
                if (++i % 20 == 0)
                    await Dispatcher.Yield(DispatcherPriority.Background);
            }
        }

        private BitmapSource? PobierzAvatar(string userId, string nazwa)
        {
            if (_avatarCache.TryGetValue(userId, out var cached)) return cached;
            System.Drawing.Image? img = null;
            try
            {
                img = UserAvatarManager.GetAvatarRounded(userId, 32)
                      ?? UserAvatarManager.GenerateDefaultAvatar(string.IsNullOrWhiteSpace(nazwa) ? userId : nazwa, userId, 32);
            }
            catch { }
            if (img == null) return null;
            BitmapSource bmp;
            try { bmp = ImageToBitmap(img); }
            finally { img.Dispose(); }
            _avatarCache[userId] = bmp;
            return bmp;
        }

        private static BitmapSource ImageToBitmap(System.Drawing.Image img)
        {
            using var ms = new MemoryStream();
            img.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.StreamSource = ms;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        // ── Filtry ───────────────────────────────────────────────────────────
        private void Filtr_Changed(object sender, RoutedEventArgs e)
        {
            if (!_ready) return;
            _debounce.Stop();
            _debounce.Start();
        }

        // ── Akcje toolbar ────────────────────────────────────────────────────
        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            bool schemat = await _service.CzySchematIstniejeAsync();
            banerSchema.Visibility = schemat ? Visibility.Collapsed : Visibility.Visible;
            await OdswiezWszystkoAsync();
        }

        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
            => new KontraktyDashboardWindow { Owner = this }.Show();

        private async void BtnNowy_Click(object sender, RoutedEventArgs e)
        {
            var w = new KontraktKreatorWindow() { Owner = this };
            w.ShowDialog();
            if (w.Zapisano) await OdswiezWszystkoAsync();
        }

        private async void OtworzKreatorSeryjny()
        {
            var w = new KontraktKreatorWindow(trybSeryjny: true) { Owner = this };
            w.ShowDialog();
            if (w.Zapisano) await OdswiezWszystkoAsync();
        }

        private async void BtnPrzedluz_Click(object sender, RoutedEventArgs e) => await PrzedluzAsync();

        private void BtnAlerty_Click(object sender, RoutedEventArgs e)
            => new KontraktyAlertyWindow { Owner = this }.Show();

        private void BtnNumeracja_Click(object sender, RoutedEventArgs e)
            => new KontraktyNumeracjaWindow { Owner = this }.ShowDialog();

        private void BtnRanking_Click(object sender, RoutedEventArgs e)
            => new KontraktyRankingWindow { Owner = this }.Show();

        private void BtnAneksy_Click(object sender, RoutedEventArgs e)
            => new KontraktyAneksyWindow { Owner = this }.ShowDialog();

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var c = await _service.GetComplianceAsync();
                var kontrakty = await _service.GetKontraktyAsync(null, "AKTYWNE", "ARIMR_3LAT", true, null);
                var trend = await _service.GetComplianceTrendAsync(365);

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Zapisz raport ARiMR (PDF)",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Raport_ARiMR_{DateTime.Now:yyyy-MM-dd}.pdf"
                };
                if (dlg.ShowDialog() != true) return;

                KontraktyPdfExport.GenerujRaportArimr(dlg.FileName, c, kontrakty, trend);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd generowania PDF: " + ex.Message, "Export ARiMR",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Dg_DoubleClick(object sender, MouseButtonEventArgs e) => await OtworzKarteAsync();

        // ── Menu kontekstowe (PPM) ───────────────────────────────────────────
        private void Dg_PreviewRightDown(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep is not DataGridRow) dep = VisualTreeHelper.GetParent(dep);
            if (dep is DataGridRow row) row.IsSelected = true;
        }

        private async void Ctx_Karta_Click(object sender, RoutedEventArgs e) => await OtworzKarteAsync();
        private async void Ctx_Historia_Click(object sender, RoutedEventArgs e) => await OtworzKarteAsync();
        private async void Ctx_Przedluz_Click(object sender, RoutedEventArgs e) => await PrzedluzAsync();

        private async void Ctx_Word_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontrakty.SelectedItem is not KontraktListItem k) return;
            new KontraktyKartaWindow(k.Id, autoGenerujWord: true) { Owner = this }.ShowDialog();
            await OdswiezWszystkoAsync();
        }

        private void Ctx_Kopiuj_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontrakty.SelectedItem is KontraktListItem k)
                try { Clipboard.SetText(k.NumerKontraktu); } catch { }
        }

        private void Ctx_Plik_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontrakty.SelectedItem is not KontraktListItem k) return;
            string? plik = !string.IsNullOrWhiteSpace(k.SciezkaPdfSkan) ? k.SciezkaPdfSkan : k.SciezkaWord;
            if (string.IsNullOrWhiteSpace(plik))
            {
                MessageBox.Show("Brak podpiętego pliku dla tego kontraktu.", "Kontrakty",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                if (File.Exists(plik)) Process.Start(new ProcessStartInfo(plik) { UseShellExecute = true });
                else MessageBox.Show("Plik nie istnieje:\n" + plik, "Kontrakty", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się otworzyć pliku: " + ex.Message, "Kontrakty",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ── Skróty klawiszowe (7.1) ──────────────────────────────────────────
        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            bool shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

            // Ctrl+F — fokus na pole szukania
            if (ctrl && e.Key == Key.F) { txtSzukaj.Focus(); txtSzukaj.SelectAll(); e.Handled = true; return; }
            // Ctrl+Shift+N — nowy kontrakt w trybie seryjnym (wprowadzanie hurtowe)
            if (ctrl && shift && e.Key == Key.N) { OtworzKreatorSeryjny(); e.Handled = true; return; }
            // Ctrl+N — nowy kontrakt
            if (ctrl && e.Key == Key.N) { BtnNowy_Click(this, new RoutedEventArgs()); e.Handled = true; return; }
            // Ctrl+R — przedłuż zaznaczony
            if (ctrl && e.Key == Key.R) { BtnPrzedluz_Click(this, new RoutedEventArgs()); e.Handled = true; return; }
            // Ctrl+E — eksport ARiMR PDF
            if (ctrl && e.Key == Key.E) { BtnExport_Click(this, new RoutedEventArgs()); e.Handled = true; return; }
            // F5 — odśwież
            if (e.Key == Key.F5) { BtnOdswiez_Click(this, new RoutedEventArgs()); e.Handled = true; return; }

            // Enter — otwórz kartę zaznaczonego (gdy fokus nie jest w polu tekstowym)
            if (e.Key == Key.Enter && Keyboard.FocusedElement is not TextBox && dgKontrakty.SelectedItem is KontraktListItem)
            { _ = OtworzKarteAsync(); e.Handled = true; return; }

            // Esc — wyczyść szukanie, jeśli coś wpisano
            if (e.Key == Key.Escape && !string.IsNullOrEmpty(txtSzukaj.Text))
            { txtSzukaj.Clear(); e.Handled = true; return; }
        }

        // ── Wspólne ──────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task OtworzKarteAsync()
        {
            if (dgKontrakty.SelectedItem is not KontraktListItem k) return;
            new KontraktyKartaWindow(k.Id) { Owner = this }.ShowDialog();
            await OdswiezWszystkoAsync();
        }

        private async System.Threading.Tasks.Task PrzedluzAsync()
        {
            if (dgKontrakty.SelectedItem is not KontraktListItem k)
            {
                MessageBox.Show("Zaznacz na liście kontrakt do przedłużenia.", "Kontrakty",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var w = new KontraktyEditorWindow(EditorMode.Przedluzenie, k.Id) { Owner = this };
            if (w.ShowDialog() == true) await OdswiezWszystkoAsync();
        }

        private async System.Threading.Tasks.Task OdswiezWszystkoAsync()
        {
            await ZaladujListeAsync();
            await ZaladujInwentarzAsync();
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static string? TagOf(ComboBox cb) => (cb.SelectedItem as ComboBoxItem)?.Tag?.ToString();

        private static string Odmiana(int n, string f1, string f234, string f5)
        {
            if (n == 1) return f1;
            int last = n % 10, last2 = n % 100;
            if (last >= 2 && last <= 4 && (last2 < 12 || last2 > 14)) return f234;
            return f5;
        }
    }
}
