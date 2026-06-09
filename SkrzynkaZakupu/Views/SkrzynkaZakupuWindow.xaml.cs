using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using Kalendarz1.SkrzynkaZakupu.Models;
using Kalendarz1.SkrzynkaZakupu.Services;

namespace Kalendarz1.SkrzynkaZakupu.Views
{
    public partial class SkrzynkaZakupuWindow : Window
    {
        private MailAccountSettings _cfg = new();
        private ImapMailService _imap = null!;
        private MailReadStateService _readState = null!;
        private readonly MailContactsService _kontaktyService = new();

        private readonly ObservableCollection<MailFolderModel> _foldery = new();
        private readonly ObservableCollection<MailMessageModel> _maile = new();
        private MailBodyModel? _aktualnaTresc;
        private bool _webReady;
        private bool _laduje;
        private bool _bezPodgladu;   // tłumi auto-podgląd treści przy programowym zaznaczeniu
        private System.Windows.Point _dragStart;
        private bool _dragGotowy;

        private ICollectionView _widokMaili = null!;
        private string _szukaj = "";
        private string _filtr = "all";   // all / unread / flag / att
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(3) };
        private ImapIdleMonitor? _idle;
        private readonly DispatcherTimer _snackTimer = new() { Interval = TimeSpan.FromSeconds(7) };
        private Func<Task>? _undoAction;

        public SkrzynkaZakupuWindow()
        {
            InitializeComponent();
            LstFoldery.ItemsSource = _foldery;
            _widokMaili = CollectionViewSource.GetDefaultView(_maile);
            _widokMaili.Filter = FiltrMaila;
            _widokMaili.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MailMessageModel.Grupa)));
            LstMaile.ItemsSource = _widokMaili;
            Loaded += SkrzynkaZakupuWindow_Loaded;
            Closed += async (_, _) =>
            {
                _timer.Stop(); _snackTimer.Stop();
                if (_idle != null) await _idle.StopAsync();
                if (_imap != null) await _imap.DisconnectQuietAsync();
            };
            KeyDown += SkrzynkaZakupuWindow_KeyDown;
        }

        private void SkrzynkaZakupuWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            bool ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;
            bool wPolu = System.Windows.Input.Keyboard.FocusedElement is TextBox;

            if (e.Key == System.Windows.Input.Key.F5) { BtnOdswiez_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == System.Windows.Input.Key.N) { BtnNapisz_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == System.Windows.Input.Key.R && BtnOdpowiedz.IsEnabled) { BtnOdpowiedz_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (ctrl && e.Key == System.Windows.Input.Key.F) { TxtSzukaj.Focus(); TxtSzukaj.SelectAll(); e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.Delete && !wPolu && BtnUsun.IsEnabled) { BtnUsun_Click(this, new RoutedEventArgs()); e.Handled = true; }
        }

        private bool FiltrMaila(object o)
        {
            if (o is not MailMessageModel m) return true;
            switch (_filtr)
            {
                case "unread": if (m.IsReadLocal) return false; break;
                case "flag": if (!m.IsFlagged) return false; break;
                case "att": if (!m.HasAttachments) return false; break;
            }
            return PasujeDoSzukania(m);
        }

        private bool PasujeDoSzukania(MailMessageModel m)
        {
            if (string.IsNullOrWhiteSpace(_szukaj)) return true;
            const StringComparison OIC = StringComparison.OrdinalIgnoreCase;
            var wolny = new List<string>();
            foreach (var tk in _szukaj.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var low = tk.ToLowerInvariant();
                if (low.StartsWith("from:"))
                {
                    var v = tk.Substring(5);
                    if (v.Length > 0 && m.FromEmail.IndexOf(v, OIC) < 0 && m.From.IndexOf(v, OIC) < 0) return false;
                }
                else if (low.StartsWith("subject:") || low.StartsWith("temat:"))
                {
                    var v = tk.Substring(tk.IndexOf(':') + 1);
                    if (v.Length > 0 && m.Subject.IndexOf(v, OIC) < 0) return false;
                }
                else if (low is "has:attachment" or "has:załącznik" or "has:zalacznik") { if (!m.HasAttachments) return false; }
                else if (low == "is:unread") { if (m.IsReadLocal) return false; }
                else if (low == "is:read") { if (!m.IsReadLocal) return false; }
                else if (low is "is:flagged" or "is:starred") { if (!m.IsFlagged) return false; }
                else wolny.Add(tk);
            }
            if (wolny.Count > 0)
            {
                var q = string.Join(" ", wolny);
                if (m.From.IndexOf(q, OIC) < 0 && m.Subject.IndexOf(q, OIC) < 0 && m.FromEmail.IndexOf(q, OIC) < 0) return false;
            }
            return true;
        }

        private void Chip_Checked(object sender, RoutedEventArgs e)
        {
            if (_widokMaili == null) return;
            _filtr = (sender as RadioButton)?.Tag as string ?? "all";
            _widokMaili.Refresh();
        }

        private void LstMaile_RightDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // zaznacz wiadomość pod kursorem, żeby menu kontekstowe działało na właściwej pozycji
            var src = e.OriginalSource as System.Windows.DependencyObject;
            while (src != null && src is not ListBoxItem)
                src = System.Windows.Media.VisualTreeHelper.GetParent(src);
            if (src is ListBoxItem item) item.IsSelected = true;
        }

        private async void BtnOznaczWszystkie_Click(object sender, RoutedEventArgs e)
        {
            if (LstFoldery.SelectedItem is not MailFolderModel f) return;
            var doOznaczenia = _maile.Where(m => !m.IsReadLocal).Select(m => m.Uid).ToList();
            if (doOznaczenia.Count == 0) { Status("Brak nieprzeczytanych"); return; }
            await _readState.SetManyReadAsync(f.FullName, doOznaczenia, true);
            foreach (var m in _maile) m.IsReadLocal = true;
            LstMaile.Items.Refresh();
            OdswiezLicznikBiezacegoFolderu();
            if (_filtr is "unread" or "flag") _widokMaili.Refresh();
            Status($"Oznaczono {doOznaczenia.Count} jako przeczytane");
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            _szukaj = TxtSzukaj.Text ?? "";
            TxtSzukajHint.Visibility = string.IsNullOrEmpty(_szukaj) ? Visibility.Visible : Visibility.Collapsed;
            _widokMaili.Refresh();
        }

        private async void SkrzynkaZakupuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 1) Konfiguracja / hasło
            if (!MailSecretsStore.MaConfig())
            {
                var dlg = new HasloSkrzynkiDialog { Owner = this };
                if (dlg.ShowDialog() != true) { Close(); return; }
            }
            _cfg = MailSecretsStore.Load();
            _imap = new ImapMailService(_cfg);
            _readState = new MailReadStateService(App.UserID ?? "?");
            TxtKonto.Text = _cfg.Email;

            // 2) WebView2
            try
            {
                await WebBody.EnsureCoreWebView2Async();
                _webReady = true;
            }
            catch { _webReady = false; }

            await ZaladujFolderyAsync();

            // Auto-odświeżanie bieżącego folderu co 3 min (bez gubienia otwartej wiadomości)
            _timer.Tick += async (_, _) => await AutoOdswiezAsync();
            _timer.Start();

            _snackTimer.Tick += (_, _) => UkryjSnackbar();

            // Live-push nowej poczty (IMAP IDLE, osobne połączenie)
            UruchomMonitor();
        }

        private void UruchomMonitor()
        {
            _idle = new ImapIdleMonitor(_cfg);
            _idle.NowaWiadomosc += OnNowaWiadomosc;
            _idle.Start();
        }

        private void OnNowaWiadomosc()
        {
            Dispatcher.Invoke(async () =>
            {
                if (LstFoldery.SelectedItem is MailFolderModel f &&
                    f.FullName.Equals("INBOX", StringComparison.OrdinalIgnoreCase))
                    await AutoOdswiezAsync();
                PokazSnackbar("📬  Nowa wiadomość w skrzynce", null);
            });
        }

        private async Task AutoOdswiezAsync()
        {
            if (_laduje || LstFoldery.SelectedItem is not MailFolderModel f) return;
            uint? zaznaczony = (LstMaile.SelectedItem as MailMessageModel)?.Uid;
            await ZaladujMaileAsync(f, cichoBezPodgladu: true);
            // przywróć zaznaczenie bez ponownego pobierania treści
            if (zaznaczony.HasValue)
            {
                var m = _maile.FirstOrDefault(x => x.Uid == zaznaczony.Value);
                if (m != null)
                {
                    _bezPodgladu = true;
                    LstMaile.SelectedItem = m;
                    _bezPodgladu = false;
                }
            }
        }

        private async Task ZaladujFolderyAsync()
        {
            Status("Ładuję foldery…");
            try
            {
                var foldery = await _imap.GetFoldersAsync();
                // Dokładne nieprzeczytane per-user liczymy dopiero po wejściu do folderu
                // (Unread serwerowy z GetFoldersAsync służy jako wstępne przybliżenie).
                _foldery.Clear();
                foreach (var f in foldery) _foldery.Add(f);
                if (_foldery.Count > 0) LstFoldery.SelectedIndex = 0;
                Status("Gotowe");
            }
            catch (Exception ex)
            {
                Status("Błąd folderów: " + ex.Message);
                MessageBox.Show("Nie udało się połączyć ze skrzynką:\n\n" + ex.Message,
                    "Skrzynka Zakupu", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void LstFoldery_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstFoldery.SelectedItem is MailFolderModel f)
                await ZaladujMaileAsync(f);
        }

        private async Task ZaladujMaileAsync(MailFolderModel folder, bool cichoBezPodgladu = false)
        {
            if (_laduje) return;
            _laduje = true;
            if (!cichoBezPodgladu) { Status($"Ładuję: {folder.DisplayName}…"); ListLoading.Visibility = Visibility.Visible; }
            _maile.Clear();
            if (!cichoBezPodgladu) WyczyscPodglad();
            try
            {
                var msgs = await _imap.GetMessagesAsync(folder.FullName, 80);
                var read = await _readState.GetReadUidsAsync(folder.FullName);
                foreach (var m in msgs)
                {
                    m.IsReadLocal = read.Contains(m.Uid);
                    _maile.Add(m);
                }
                folder.Unread = _maile.Count(x => !x.IsReadLocal);
                LstFoldery.Items.Refresh();
                TxtPusto.Visibility = _maile.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                UpdateUnreadBadge();
                _ = AutoZbierzKontaktyAsync();   // w tle: każdy nadawca/odbiorca → książka adresowa
                Status($"{folder.DisplayName}: {_maile.Count} wiadomości, {folder.Unread} nieprzeczytanych");
            }
            catch (Exception ex)
            {
                Status("Błąd wczytywania: " + ex.Message);
            }
            finally { _laduje = false; ListLoading.Visibility = Visibility.Collapsed; }
        }

        private async void LstMaile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_bezPodgladu) return;
            if (LstMaile.SelectedItem is not MailMessageModel m) return;
            await PokazTrescAsync(m, oznaczPrzeczytane: true);
        }

        private async void LstMaile_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // dwuklik = otwórz wiadomość w osobnym oknie (jak Outlook)
            if (LstMaile.SelectedItem is MailMessageModel) await OtworzWoknie();
        }

        private async Task PokazTrescAsync(MailMessageModel m, bool oznaczPrzeczytane)
        {
            Status("Pobieram treść…");
            BodyLoading.Visibility = Visibility.Visible;
            try
            {
                var body = await _imap.GetBodyAsync(m.FolderFullName, m.Uid);
                if (body == null) { Status("Nie udało się pobrać treści."); return; }
                _aktualnaTresc = body;

                TxtWybierz.Visibility = Visibility.Collapsed;
                PodgladHeader.Visibility = Visibility.Visible;
                PodgladAkcje.Visibility = Visibility.Visible;

                TxtPodgladTemat.Text = body.Subject;
                TxtPodgladOd.Text = string.IsNullOrWhiteSpace(body.From) ? body.FromEmail : body.From;
                TxtPodgladDo.Text = string.IsNullOrWhiteSpace(body.To)
                    ? body.FromEmail
                    : $"{body.FromEmail}   •   do: {body.To}";
                TxtPodgladData.Text = body.Date.ToString("dddd, dd MMMM yyyy, HH:mm");

                // awatar nadawcy
                PodgladInicjaly.Text = Models.MailAvatar.Inicjaly(body.From, body.FromEmail);
                try
                {
                    var hex = Models.MailAvatar.Kolor(body.FromEmail);
                    PodgladAvatar.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
                }
                catch { }

                LstZalaczniki.ItemsSource = body.Attachments;
                PanelZalaczniki.Visibility = body.Attachments.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                TxtZalLabel.Text = body.Attachments.Count == 1 ? "Załącznik (1)" : $"Załączniki ({body.Attachments.Count})";
                UstawAkcje(true);

                RenderujTresc(body);
                FadeIn(PodgladHeader);

                if (oznaczPrzeczytane && !m.IsReadLocal)
                {
                    m.IsReadLocal = true;
                    await _readState.SetReadAsync(m.FolderFullName, m.Uid, true);
                    LstMaile.Items.Refresh();
                    OdswiezLicznikBiezacegoFolderu();
                }
                Status("Gotowe");
            }
            catch (Exception ex)
            {
                Status("Błąd treści: " + ex.Message);
            }
            finally { BodyLoading.Visibility = Visibility.Collapsed; }
        }

        private static void FadeIn(UIElement el)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
            el.BeginAnimation(OpacityProperty, anim);
        }

        private void RenderujTresc(MailBodyModel body)
        {
            string html;
            if (body.IsHtml)
                html = body.HtmlBody;
            else
                html = "<pre style='font-family:Segoe UI,Arial; font-size:13px; white-space:pre-wrap; word-wrap:break-word;'>"
                       + System.Net.WebUtility.HtmlEncode(body.TextBody) + "</pre>";

            if (_webReady)
            {
                WebBody.Visibility = Visibility.Visible;
                try { WebBody.NavigateToString(WrapHtml(html)); } catch { }
            }
        }

        private static string WrapHtml(string inner)
            => "<!DOCTYPE html><html><head><meta charset='utf-8'>" +
               "<meta name='viewport' content='width=device-width, initial-scale=1'>" +
               "<base target='_blank'>" +
               "<style>" +
               "html,body{margin:0;padding:0;}" +
               "body{font-family:'Segoe UI',Arial,sans-serif;font-size:14px;line-height:1.6;color:#1f2933;" +
               "padding:18px 22px;max-width:860px;-webkit-font-smoothing:antialiased;}" +
               "a{color:#2E7D32;}img{max-width:100%;height:auto;}" +
               "blockquote{margin:8px 0;padding:4px 14px;border-left:3px solid #E5E9EF;color:#64748b;}" +
               "pre{white-space:pre-wrap;word-wrap:break-word;font-family:'Segoe UI',Arial;}" +
               "table{max-width:100%;}" +
               "::-webkit-scrollbar{width:10px;height:10px;}::-webkit-scrollbar-thumb{background:#CBD5E1;border-radius:5px;}" +
               "</style></head><body>" +
               inner + "</body></html>";

        // ---------------- TOOLBAR ----------------

        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            if (LstFoldery.SelectedItem is MailFolderModel f)
                await ZaladujMaileAsync(f);
            else
                await ZaladujFolderyAsync();
        }

        private void BtnNapisz_Click(object sender, RoutedEventArgs e)
        {
            var okno = new OknoNowaWiadomosc(_cfg) { Owner = this };
            okno.ShowDialog();
        }

        private void BtnOdpowiedz_Click(object sender, RoutedEventArgs e)
        {
            if (_aktualnaTresc == null) { Status("Najpierw wybierz wiadomość."); return; }
            var okno = new OknoNowaWiadomosc(_cfg) { Owner = this };
            okno.UstawTryb("reply", _aktualnaTresc);
            okno.ShowDialog();
        }

        private void BtnPrzekaz_Click(object sender, RoutedEventArgs e)
        {
            if (_aktualnaTresc == null) { Status("Najpierw wybierz wiadomość."); return; }
            var okno = new OknoNowaWiadomosc(_cfg) { Owner = this };
            okno.UstawTryb("forward", _aktualnaTresc);
            okno.ShowDialog();
        }

        private async void BtnPrzeczytane_Click(object sender, RoutedEventArgs e) => await OznaczAsync(true);
        private async void BtnNieprzeczytane_Click(object sender, RoutedEventArgs e) => await OznaczAsync(false);

        private List<MailMessageModel> Zaznaczone() => LstMaile.SelectedItems.Cast<MailMessageModel>().ToList();

        private async Task OznaczAsync(bool przeczytane)
        {
            var sel = Zaznaczone();
            if (sel.Count == 0) { Status("Zaznacz wiadomość."); return; }
            var folder = sel[0].FolderFullName;
            foreach (var m in sel) m.IsReadLocal = przeczytane;
            await _readState.SetManyReadAsync(folder, sel.Select(m => m.Uid), przeczytane);
            LstMaile.Items.Refresh();
            OdswiezLicznikBiezacegoFolderu();
            if (_filtr == "unread") _widokMaili.Refresh();
            Status(sel.Count == 1 ? "Oznaczono" : $"Oznaczono {sel.Count}");
        }

        private async void BtnPrzenies_Click(object sender, RoutedEventArgs e)
        {
            var sel = Zaznaczone();
            if (sel.Count == 0) { Status("Zaznacz wiadomość."); return; }
            var zrodlo = sel[0].FolderFullName;
            var inne = _foldery.Where(f => !f.FullName.Equals(zrodlo, StringComparison.OrdinalIgnoreCase)).ToList();
            if (inne.Count == 0) return;

            var dlg = new WyborFolderuDialog(inne) { Owner = this };
            if (dlg.ShowDialog() != true || dlg.Wybrany == null) return;
            await PrzeniesAsync(sel, dlg.Wybrany);
        }

        private async Task PrzeniesAsync(List<MailMessageModel> sel, MailFolderModel cel)
        {
            if (sel.Count == 0) return;
            Status("Przenoszę…");
            var cofnij = new List<(string orig, string cel, uint newUid)>();
            foreach (var m in sel)
            {
                try
                {
                    var newUid = await _imap.MoveAsync(m.FolderFullName, m.Uid, cel.FullName);
                    if (newUid.HasValue) cofnij.Add((m.FolderFullName, cel.FullName, newUid.Value));
                    _maile.Remove(m);
                }
                catch (Exception ex) { Status("Błąd przenoszenia: " + ex.Message); }
            }
            WyczyscPodglad();
            OdswiezLicznikBiezacegoFolderu();
            PokazSnackbar($"Przeniesiono do {cel.DisplayName}: {sel.Count}",
                cofnij.Count == 0 ? null : async () =>
                {
                    foreach (var c in cofnij) { try { await _imap.MoveAsync(c.cel, c.newUid, c.orig); } catch { } }
                    if (LstFoldery.SelectedItem is MailFolderModel f) await ZaladujMaileAsync(f);
                });
        }

        private async void BtnUsun_Click(object sender, RoutedEventArgs e)
        {
            var sel = Zaznaczone();
            if (sel.Count == 0) { Status("Zaznacz wiadomość."); return; }
            Status("Usuwam…");
            var cofnij = new List<(string orig, string trash, uint newUid)>();
            foreach (var m in sel)
            {
                try
                {
                    var (trash, newUid) = await _imap.DeleteToTrashAsync(m.FolderFullName, m.Uid);
                    if (newUid.HasValue && !string.IsNullOrEmpty(trash)) cofnij.Add((m.FolderFullName, trash, newUid.Value));
                    _maile.Remove(m);
                }
                catch (Exception ex) { Status("Błąd usuwania: " + ex.Message); }
            }
            WyczyscPodglad();
            OdswiezLicznikBiezacegoFolderu();
            PokazSnackbar(sel.Count == 1 ? "Przeniesiono do Kosza" : $"Przeniesiono do Kosza: {sel.Count}",
                cofnij.Count == 0 ? null : async () =>
                {
                    foreach (var c in cofnij) { try { await _imap.MoveAsync(c.trash, c.newUid, c.orig); } catch { } }
                    if (LstFoldery.SelectedItem is MailFolderModel f) await ZaladujMaileAsync(f);
                });
        }

        // ---- gwiazdki / reply-all / otwórz w oknie / druk ----
        private async void Gwiazdka_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is MailMessageModel m) await PrzelaczGwiazdke(m);
        }

        private async void BtnGwiazdkaMenu_Click(object sender, RoutedEventArgs e)
        {
            if (LstMaile.SelectedItem is MailMessageModel m) await PrzelaczGwiazdke(m);
        }

        private async Task PrzelaczGwiazdke(MailMessageModel m)
        {
            bool nowy = !m.IsFlagged;
            m.IsFlagged = nowy;
            LstMaile.Items.Refresh();
            if (_filtr == "flag") _widokMaili.Refresh();
            try { await _imap.SetFlaggedAsync(m.FolderFullName, m.Uid, nowy); }
            catch (Exception ex) { Status("Błąd gwiazdki: " + ex.Message); }
        }

        private void BtnOdpowiedzWsz_Click(object sender, RoutedEventArgs e)
        {
            if (_aktualnaTresc == null) { Status("Najpierw wybierz wiadomość."); return; }
            var okno = new OknoNowaWiadomosc(_cfg) { Owner = this };
            okno.UstawTryb("replyall", _aktualnaTresc);
            okno.ShowDialog();
        }

        private async void BtnOtworzWoknie_Click(object sender, RoutedEventArgs e) => await OtworzWoknie();

        private async Task OtworzWoknie()
        {
            var body = _aktualnaTresc;
            if (body == null && LstMaile.SelectedItem is MailMessageModel m)
                body = await _imap.GetBodyAsync(m.FolderFullName, m.Uid);
            if (body == null) { Status("Wybierz wiadomość."); return; }
            new MailReaderWindow(body, _cfg).Show();
        }

        private async void BtnDrukuj_Click(object sender, RoutedEventArgs e)
        {
            if (_webReady && WebBody.CoreWebView2 != null)
                try { await WebBody.CoreWebView2.ExecuteScriptAsync("window.print();"); } catch { }
        }

        // ---- snackbar (Cofnij) ----
        private void PokazSnackbar(string text, Func<Task>? undo)
        {
            TxtSnackbar.Text = text;
            _undoAction = undo;
            BtnUndo.Visibility = undo != null ? Visibility.Visible : Visibility.Collapsed;
            Snackbar.Visibility = Visibility.Visible;
            FadeIn(Snackbar);
            _snackTimer.Stop(); _snackTimer.Start();
        }

        private void UkryjSnackbar()
        {
            _snackTimer.Stop();
            Snackbar.Visibility = Visibility.Collapsed;
            _undoAction = null;
        }

        private async void BtnUndo_Click(object sender, RoutedEventArgs e)
        {
            var akcja = _undoAction;
            UkryjSnackbar();
            if (akcja != null) { Status("Cofam…"); await akcja(); Status("Cofnięto"); }
        }

        // ---- drag & drop maili na foldery (jak Outlook) ----
        private void LstMaile_PreLeftDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
            _dragGotowy = ZnajdzListBoxItem(e.OriginalSource as System.Windows.DependencyObject) != null;
        }

        private void LstMaile_PreMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_dragGotowy || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed) return;
            var poz = e.GetPosition(null);
            if (Math.Abs(poz.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(poz.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            var sel = Zaznaczone();
            if (sel.Count == 0) return;
            _dragGotowy = false;
            try { System.Windows.DragDrop.DoDragDrop(LstMaile, new DataObject("maile", sel), DragDropEffects.Move); }
            catch { }
        }

        private void LstFoldery_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent("maile") ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private async void LstFoldery_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("maile")) return;
            var item = ZnajdzListBoxItem(e.OriginalSource as System.Windows.DependencyObject);
            if (item?.DataContext is not MailFolderModel cel) return;
            if (e.Data.GetData("maile") is not List<MailMessageModel> sel || sel.Count == 0) return;
            if (sel[0].FolderFullName.Equals(cel.FullName, StringComparison.OrdinalIgnoreCase)) return;
            await PrzeniesAsync(sel, cel);
        }

        private static ListBoxItem? ZnajdzListBoxItem(System.Windows.DependencyObject? src)
        {
            while (src != null && src is not ListBoxItem)
                src = System.Windows.Media.VisualTreeHelper.GetParent(src);
            return src as ListBoxItem;
        }

        // ---- sortowanie ----
        private void BtnSortuj_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.ContextMenu != null)
            {
                b.ContextMenu.PlacementTarget = b;
                b.ContextMenu.IsOpen = true;
            }
        }

        private void Sortuj_Click(object sender, RoutedEventArgs e)
            => UstawSortowanie((sender as MenuItem)?.Tag as string ?? "date");

        private void UstawSortowanie(string tryb)
        {
            if (_widokMaili == null) return;
            using (_widokMaili.DeferRefresh())
            {
                _widokMaili.SortDescriptions.Clear();
                _widokMaili.GroupDescriptions.Clear();
                switch (tryb)
                {
                    case "sender":
                        _widokMaili.SortDescriptions.Add(new SortDescription(nameof(MailMessageModel.From), ListSortDirection.Ascending));
                        break;
                    case "subject":
                        _widokMaili.SortDescriptions.Add(new SortDescription(nameof(MailMessageModel.Subject), ListSortDirection.Ascending));
                        break;
                    case "unread":
                        _widokMaili.SortDescriptions.Add(new SortDescription(nameof(MailMessageModel.IsReadLocal), ListSortDirection.Ascending));
                        _widokMaili.SortDescriptions.Add(new SortDescription(nameof(MailMessageModel.Date), ListSortDirection.Descending));
                        break;
                    default:
                        _widokMaili.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MailMessageModel.Grupa)));
                        _widokMaili.SortDescriptions.Add(new SortDescription(nameof(MailMessageModel.Date), ListSortDirection.Descending));
                        break;
                }
            }
            Status("Sortowanie: " + tryb switch
            {
                "sender" => "nadawca",
                "subject" => "temat",
                "unread" => "nieprzeczytane u góry",
                _ => "data (najnowsze)"
            });
        }

        private void BtnKontakty_Click(object sender, RoutedEventArgs e)
        {
            var okno = new ImportKontaktowWindow(_cfg) { Owner = this };
            okno.ShowDialog();
        }

        private async void BtnUstawienia_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new HasloSkrzynkiDialog { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                if (_imap != null) await _imap.DisconnectQuietAsync();
                if (_idle != null) await _idle.StopAsync();
                _cfg = dlg.Settings;
                _imap = new ImapMailService(_cfg);
                TxtKonto.Text = _cfg.Email;
                await ZaladujFolderyAsync();
                UruchomMonitor();
            }
        }

        // ---------------- ZAŁĄCZNIKI ----------------

        private void Zalacznik_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is MailAttachmentModel att)
            {
                var dlg = new Microsoft.Win32.SaveFileDialog { FileName = att.FileName };
                if (dlg.ShowDialog() == true)
                {
                    try
                    {
                        File.WriteAllBytes(dlg.FileName, att.Content);
                        Status("Zapisano: " + Path.GetFileName(dlg.FileName));
                    }
                    catch (Exception ex) { Status("Błąd zapisu: " + ex.Message); }
                }
            }
        }

        private void BtnZapiszWszystkie_Click(object sender, RoutedEventArgs e)
        {
            if (_aktualnaTresc == null || _aktualnaTresc.Attachments.Count == 0) { Status("Brak załączników"); return; }
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Wybierz folder do zapisu wszystkich załączników"
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            int ok = 0;
            foreach (var att in _aktualnaTresc.Attachments)
            {
                try
                {
                    var sciezka = UnikalnaSciezka(dlg.SelectedPath, att.FileName);
                    File.WriteAllBytes(sciezka, att.Content);
                    ok++;
                }
                catch { /* pomiń problematyczny plik */ }
            }
            Status($"Zapisano {ok} z {_aktualnaTresc.Attachments.Count} załączników → {dlg.SelectedPath}");
        }

        /// <summary>Zwraca ścieżkę z unikalną nazwą (dokłada „ (2)" przy kolizji).</summary>
        private static string UnikalnaSciezka(string folder, string nazwa)
        {
            var bezpieczna = string.Concat(nazwa.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(bezpieczna)) bezpieczna = "zalacznik";
            var pelna = Path.Combine(folder, bezpieczna);
            if (!File.Exists(pelna)) return pelna;

            var baza = Path.GetFileNameWithoutExtension(bezpieczna);
            var ext = Path.GetExtension(bezpieczna);
            for (int i = 2; i < 1000; i++)
            {
                var kandydat = Path.Combine(folder, $"{baza} ({i}){ext}");
                if (!File.Exists(kandydat)) return kandydat;
            }
            return pelna;
        }

        // ---------------- helpers ----------------

        private void OdswiezLicznikBiezacegoFolderu()
        {
            if (LstFoldery.SelectedItem is MailFolderModel f)
            {
                f.Unread = _maile.Count(x => !x.IsReadLocal);
                LstFoldery.Items.Refresh();
            }
            UpdateUnreadBadge();
        }

        private void UpdateUnreadBadge()
        {
            int total = _foldery.Sum(f => f.Unread);
            if (total > 0)
            {
                TxtUnreadTotal.Text = total == 1 ? "1 nieprzeczytana" : $"{total} nieprzeczytanych";
                BadgeUnread.Visibility = Visibility.Visible;
            }
            else BadgeUnread.Visibility = Visibility.Collapsed;
        }

        private void WyczyscPodglad()
        {
            _aktualnaTresc = null;
            TxtPodgladTemat.Text = "";
            TxtPodgladOd.Text = "";
            TxtPodgladDo.Text = "";
            TxtPodgladData.Text = "";
            LstZalaczniki.ItemsSource = null;
            PanelZalaczniki.Visibility = Visibility.Collapsed;
            PodgladHeader.Visibility = Visibility.Collapsed;
            PodgladAkcje.Visibility = Visibility.Collapsed;
            TxtWybierz.Visibility = Visibility.Visible;
            UstawAkcje(false);
            if (_webReady) { try { WebBody.NavigateToString("<html><body></body></html>"); } catch { } }
        }

        /// <summary>Włącza/wyłącza przyciski akcji zależne od wybranej wiadomości.</summary>
        private void UstawAkcje(bool on)
        {
            BtnOdpowiedz.IsEnabled = on;
            BtnPrzekaz.IsEnabled = on;
            BtnPrzeczytane.IsEnabled = on;
            BtnNieprzeczytane.IsEnabled = on;
            BtnPrzenies.IsEnabled = on;
            BtnUsun.IsEnabled = on;
        }

        /// <summary>Pasywnie dopisuje adresy z bieżącej listy do wspólnej książki (do podpowiedzi przy pisaniu).</summary>
        private async Task AutoZbierzKontaktyAsync()
        {
            try
            {
                var kontakty = _maile.SelectMany(m => m.Kontakty).ToList();
                if (kontakty.Count > 0)
                    await _kontaktyService.UpsertManyAsync(kontakty, "auto");
            }
            catch { /* best-effort, nie przeszkadza w UI */ }
        }

        private void Status(string s) => TxtStatusBar.Text = s;
    }
}
