using Kalendarz1.Customer360.Models;
using Kalendarz1.Customer360.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Kalendarz1.Customer360
{
    public partial class Customer360Window : Window
    {
        #region Pola, stałe, konstruktor

        private readonly Customer360Service _service = new();
        private int? _selectedKlientId;
        private static readonly CultureInfo Pl = new("pl-PL");

        private const string ConnLibra =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private const string ConnHandelW =
            "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";
        private const string ConnTransport =
            "Server=192.168.0.109;Database=TransportPL;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        // Serwisy Kartoteki (reużyte do edycji/kontaktów/historii/transportu/asortymentu)
        private readonly Kalendarz1.Kartoteka.Services.KartotekaService _kartoteka;
        private readonly Kalendarz1.Kartoteka.Features.Historia.HistoriaZmianService _historia;
        private Kalendarz1.Kartoteka.Models.OdbiorcaHandlowca? _edytowany;  // bieżący klient (dane własne)
        private KlientHeader? _hdr;  // bieżący nagłówek (do szybkich akcji)
        private List<int>? _nawigacja;  // lista klientów do nawigacji ◀▶ (kolejność z listy Kartoteki)
        // Dane do drill-downu po kliknięciu w wykres
        private List<Models.FakturaDetail> _fakturyDet = new();
        private List<OrderHistoryItem> _history = new();
        private int _okres = 0;  // miesięcy wstecz dla list/wykresów (0 = cała historia)
        private List<Models.WeryfikacjaTowar> _werTowary = new();
        private string _werFiltr = "ALL";  // ALL / UCIETE / WIECEJ / BRAK / ZGODNE
        private List<MonthlyStats> _monthlyData = new();   // do drill-downu z wykresu obrotu (LiveCharts)
        private bool _chartMonthlyWired;
        private Services.Customer360Snapshot? _snapshot;   // migawka do eksportu PDF
        private bool _analizaTabLoaded;                    // lazy-load grupy Analiza (historia/transport/asortyment)

        private static readonly string[] MiesSkrot = { "sty", "lut", "mar", "kwi", "maj", "cze", "lip", "sie", "wrz", "paź", "lis", "gru" };
        private static readonly string[] MiesPelny = { "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec", "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień" };

        // Jeden konwerter hex->Brush dla calego okna — eliminuje 9 lokalnych kopii i ryzyko driftu fallbacku.
        private static readonly BrushConverter _bc = new();
        private static Brush B(string hex)
        {
            try { return (Brush)_bc.ConvertFromString(hex)!; }
            catch { return Brushes.Gray; }
        }

        public Customer360Window() : this(null, null) { }
        public Customer360Window(int? preselectKlientId) : this(preselectKlientId, null) { }
        public Customer360Window(int? preselectKlientId, List<int>? nawigacja)
        {
            InitializeComponent();
            try { WindowIconHelper.SetIcon(this); } catch { }
            _selectedKlientId = preselectKlientId;
            _nawigacja = nawigacja;
            _kartoteka = new Kalendarz1.Kartoteka.Services.KartotekaService(ConnLibra, ConnHandelW, ConnTransport);
            _historia = new Kalendarz1.Kartoteka.Features.Historia.HistoriaZmianService(ConnLibra);
            PreviewKeyDown += Customer360_PreviewKeyDown;
        }

        // Nawigacja klientów (◀ ▶ oraz Ctrl+←/→)
        #endregion

        #region Pasek narzędzi, nawigacja, eventy UI

        private async Task NawigujAsync(int kierunek)
        {
            if (_nawigacja == null || _nawigacja.Count == 0 || !_selectedKlientId.HasValue) return;
            int idx = _nawigacja.IndexOf(_selectedKlientId.Value);
            if (idx < 0) return;
            int nowy = idx + kierunek;
            if (nowy < 0 || nowy >= _nawigacja.Count) return;
            _selectedKlientId = _nawigacja[nowy];
            await LoadKlientAsync(_selectedKlientId.Value);
        }

        private void OdswiezNawigacje()
        {
            if (_nawigacja == null || _nawigacja.Count == 0 || !_selectedKlientId.HasValue)
            {
                BtnPrev.IsEnabled = false; BtnNext.IsEnabled = false;
                LblNawPozycja.Text = "";
                return;
            }
            int idx = _nawigacja.IndexOf(_selectedKlientId.Value);
            BtnPrev.IsEnabled = idx > 0;
            BtnNext.IsEnabled = idx >= 0 && idx < _nawigacja.Count - 1;
            LblNawPozycja.Text = idx >= 0 ? $"{idx + 1} / {_nawigacja.Count}" : "";
        }

        private async void BtnPrev_Click(object sender, RoutedEventArgs e) => await NawigujAsync(-1);
        private async void BtnNext_Click(object sender, RoutedEventArgs e) => await NawigujAsync(+1);

        private async void CmbOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _okres = CmbOkres.SelectedIndex switch { 1 => 12, 2 => 6, 3 => 3, _ => 0 };
            if (_selectedKlientId.HasValue && IsLoaded) await LoadKlientAsync(_selectedKlientId.Value);
        }

        private void BtnPorownaj_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedKlientId.HasValue || _hdr == null)
            {
                MessageBox.Show(this, "Najpierw otwórz klienta.", "Porównanie", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new PorownanieKlientowWindow(_selectedKlientId.Value, _hdr.Nazwa) { Owner = this };
            dlg.Show();
        }

        private void ZapiszOstatnioOtwarty()
        {
            if (_selectedKlientId.HasValue && _hdr != null)
                Services.RecentClientsStore.Add(_selectedKlientId.Value, _hdr.Nazwa);
        }

        // ── Ostatnio otwierani klienci (menu kontekstowe) ──
        private void BtnOstatni_Click(object sender, RoutedEventArgs e)
        {
            var recent = Services.RecentClientsStore.Get();
            var menu = new ContextMenu();
            if (recent.Count == 0)
            {
                menu.Items.Add(new MenuItem { Header = "(brak historii)", IsEnabled = false });
            }
            else
            {
                foreach (var r in recent)
                {
                    var mi = new MenuItem { Header = $"{r.Nazwa}   ·   {r.Kiedy:dd.MM HH:mm}" };
                    int id = r.Id;
                    mi.Click += async (s, ev) => { _selectedKlientId = id; await LoadKlientAsync(id); };
                    menu.Items.Add(mi);
                }
            }
            menu.PlacementTarget = BtnOstatni;
            menu.IsOpen = true;
        }

        // Aktywny grid = pierwszy widoczny DataGrid w aktualnie wybranej (zagnieżdżonej) zakładce
        private DataGrid? AktywnyGrid()
        {
            var content = MainTabs?.SelectedContent as DependencyObject;
            return content == null ? GridTopTowary : (ZnajdzWidocznyGrid(content) ?? GridTopTowary);
        }

        private static DataGrid? ZnajdzWidocznyGrid(DependencyObject root)
        {
            int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
                if (child is DataGrid g && g.IsVisible) return g;
                var found = ZnajdzWidocznyGrid(child);
                if (found != null) return found;
            }
            return null;
        }

        private async void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl) FiltrujAktywnyGrid();
            // Lazy-load grupy po zmianie GŁÓWNEJ zakładki (nie wewnętrznej)
            if (ReferenceEquals(e.Source, MainTabs))
                await ZaladujGrupeAsync(MainTabs.SelectedIndex);
        }

        // Lazy-load danych grupy (na razie tylko Analiza = index 3)
        private async Task ZaladujGrupeAsync(int topIndex)
        {
            if (!_selectedKlientId.HasValue) return;
            if (topIndex == 3 && !_analizaTabLoaded)
            {
                _analizaTabLoaded = true;
                try { await LoadAnalizaTabAsync(_selectedKlientId.Value); }
                catch (Exception ex) { _analizaTabLoaded = false; System.Diagnostics.Debug.WriteLine("[C360 AnalizaTab] " + ex.Message); }
            }
        }

        private void TxtSzukajGrid_TextChanged(object sender, TextChangedEventArgs e) => FiltrujAktywnyGrid();

        private void FiltrujAktywnyGrid()
        {
            // Wyczyść filtry na wszystkich gridach, potem zastosuj do aktywnego
            var wszystkie = new[] { GridZamowienia, GridFakturyDetail, GridAnulowane, GridKontakty, GridHistoria, GridAsortyment, GridTopTowary };
            foreach (var g in wszystkie)
            {
                if (g?.ItemsSource != null)
                {
                    var v = System.Windows.Data.CollectionViewSource.GetDefaultView(g.ItemsSource);
                    if (v != null) v.Filter = null;
                }
            }

            var grid = AktywnyGrid();
            if (grid?.ItemsSource == null || TxtSzukajGrid == null) return;
            string q = (TxtSzukajGrid.Text ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(q)) return;

            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(grid.ItemsSource);
            if (view == null) return;
            var kolumny = grid.Columns.OfType<DataGridBoundColumn>().ToList();
            view.Filter = item =>
            {
                if (item == null) return false;
                foreach (var k in kolumny)
                {
                    if (k.Binding is System.Windows.Data.Binding b && !string.IsNullOrEmpty(b.Path?.Path))
                    {
                        var val = PobierzWlasciwosc(item, b.Path.Path);
                        if (val == null) continue;
                        string s = !string.IsNullOrEmpty(b.StringFormat) ? string.Format(Pl, b.StringFormat, val) : Convert.ToString(val, Pl) ?? "";
                        if (s.ToLowerInvariant().Contains(q)) return true;
                    }
                }
                return false;
            };
        }

        // ── Eksport aktywnej zakładki do CSV (Excel-friendly, separator ;) ──
        // Lista alertów (te same warunki co panel alertów) — do snapshotu/PDF
        private List<string> BudujListeAlertow(KlientKpi kpi, Models.WeryfikacjaSumarum wer)
        {
            var l = new List<string>();
            if (kpi.Przeterminowane > 0.01m) l.Add($"Przeterminowane {kpi.Przeterminowane:N0} zł (max {kpi.MaxDniOpoznienia} dni po terminie)");
            if (kpi.LimitKredytowy > 0 && kpi.WykorzystanieLimitProc >= 80) l.Add($"Limit kredytowy wykorzystany w {kpi.WykorzystanieLimitProc:N0}%");
            if (kpi.ChurnRiskLevel == "CRITICAL" || kpi.ChurnRiskLevel == "WARNING") l.Add($"Ryzyko odejścia: {kpi.ChurnRiskReason}");
            if (kpi.OstatnieZamowienie.HasValue && kpi.SredniCzasMiedzyZamowieniami > 0 && kpi.DniOdOstatniegoZamowienia > kpi.SredniCzasMiedzyZamowieniami * 2)
                l.Add($"{kpi.DniOdOstatniegoZamowienia} dni bez zamówienia (norma co {kpi.SredniCzasMiedzyZamowieniami:N0} dni)");
            if (wer.LiczbaTowarowUcietych > 0) l.Add($"{wer.LiczbaTowarowUcietych} towarów uciętych (zamówiono więcej niż zafakturowano)");
            if (kpi.LiczbaReklamacji12M > 0) l.Add($"{kpi.LiczbaReklamacji12M} reklamacji w 12 mies ({kpi.RelativeReklamacjeProc:N1}% obrotu)");
            return l;
        }

        // Eksport całej karty do PDF
        private void BtnEksportPdf_Click(object sender, RoutedEventArgs e)
        {
            if (_snapshot == null || !_selectedKlientId.HasValue)
            {
                MessageBox.Show(this, "Najpierw otwórz klienta.", "Eksport PDF", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                string nazwa = (_snapshot.Nazwa ?? "klient");
                foreach (var c in System.IO.Path.GetInvalidFileNameChars()) nazwa = nazwa.Replace(c, '_');
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Zapisz kartę klienta jako PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Karta360_{nazwa}_{DateTime.Now:yyyyMMdd}.pdf",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };
                if (dlg.ShowDialog(this) != true) return;

                Cursor = System.Windows.Input.Cursors.Wait;
                byte[] pdf = new Services.Customer360PdfExporter().Generate(_snapshot);
                System.IO.File.WriteAllBytes(dlg.FileName, pdf);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Nie udało się wygenerować PDF: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { Cursor = System.Windows.Input.Cursors.Arrow; }
        }

        private void BtnEksport_Click(object sender, RoutedEventArgs e)
        {
            DataGrid? grid = AktywnyGrid();
            if (grid == null || grid.Items.Count == 0)
            {
                MessageBox.Show(this, "Brak danych do eksportu w tej zakładce. Przejdź na zakładkę z tabelą (Zamówienia/Faktury/Asortyment…).",
                    "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                string nazwaKl = (_hdr?.Nazwa ?? "klient").Replace(" ", "_");
                foreach (var c in System.IO.Path.GetInvalidFileNameChars()) nazwaKl = nazwaKl.Replace(c, '_');
                string nazwaTab = (MainTabs.SelectedItem as TabItem)?.Header?.ToString() ?? "dane";
                foreach (var c in System.IO.Path.GetInvalidFileNameChars()) nazwaTab = nazwaTab.Replace(c, '_');
                string path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"C360_{nazwaKl}_{nazwaTab}_{DateTime.Now:yyyyMMdd_HHmm}.csv");

                var csv = GridDoCsv(grid);
                System.IO.File.WriteAllText(path, csv, new System.Text.UTF8Encoding(true)); // BOM → Excel PL
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Nie udało się wyeksportować: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string GridDoCsv(DataGrid grid)
        {
            string Csv(string? s)
            {
                s ??= "";
                if (s.Contains(';') || s.Contains('"') || s.Contains('\n'))
                    return "\"" + s.Replace("\"", "\"\"") + "\"";
                return s;
            }

            // Kolumny z bindingiem (pomijamy template/obrazki)
            var kolumny = grid.Columns.OfType<DataGridBoundColumn>().ToList();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(string.Join(";", kolumny.Select(k => Csv(k.Header?.ToString()))));

            foreach (var item in grid.Items)
            {
                if (item == null) continue;
                var komorki = new List<string>();
                foreach (var k in kolumny)
                {
                    string tekst = "";
                    if (k.Binding is System.Windows.Data.Binding b && !string.IsNullOrEmpty(b.Path?.Path))
                    {
                        object? val = PobierzWlasciwosc(item, b.Path.Path);
                        if (val != null)
                            tekst = !string.IsNullOrEmpty(b.StringFormat)
                                ? string.Format(Pl, b.StringFormat, val)
                                : Convert.ToString(val, Pl) ?? "";
                    }
                    komorki.Add(Csv(tekst));
                }
                sb.AppendLine(string.Join(";", komorki));
            }
            return sb.ToString();
        }

        private static object? PobierzWlasciwosc(object obj, string path)
        {
            // Obsługa zagnieżdżonych ścieżek "A.B" (np. Klient.Nazwa)
            object? cur = obj;
            foreach (var part in path.Split('.'))
            {
                if (cur == null) return null;
                var pi = cur.GetType().GetProperty(part);
                if (pi == null) return null;
                cur = pi.GetValue(cur);
            }
            return cur;
        }

        private async void Customer360_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Esc zamyka kartę; F5 odświeża. Nie przejmuj, gdy fokus jest w polu tekstowym (edycja).
            bool ctrl = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0;
            if (e.Key == System.Windows.Input.Key.Escape && !(System.Windows.Input.Keyboard.FocusedElement is TextBox))
            {
                Close();
            }
            else if (e.Key == System.Windows.Input.Key.F5 && _selectedKlientId.HasValue)
            {
                e.Handled = true;
                await LoadKlientAsync(_selectedKlientId.Value);
            }
            else if (ctrl && e.Key == System.Windows.Input.Key.Left)
            {
                e.Handled = true;
                await NawigujAsync(-1);
            }
            else if (ctrl && e.Key == System.Windows.Input.Key.Right)
            {
                e.Handled = true;
                await NawigujAsync(+1);
            }
            else if (ctrl && e.Key == System.Windows.Input.Key.E)
            {
                e.Handled = true;
                BtnEksportPdf_Click(this, new RoutedEventArgs());
            }
            else if (ctrl && e.Key == System.Windows.Input.Key.R && _selectedKlientId.HasValue)
            {
                e.Handled = true;
                Services.Customer360ScoringService.InvalidateScore(_selectedKlientId.Value);
                await LoadKlientAsync(_selectedKlientId.Value, forceScore: true);  // wymuś przeliczenie scoringu
            }
        }

        // ── Szybkie akcje nagłówka ──
        private void BtnKopiujNip_Click(object sender, RoutedEventArgs e)
        {
            string nip = _hdr?.NIP ?? "";
            if (string.IsNullOrWhiteSpace(nip)) { BtnKopiujNip.Content = "brak NIP"; return; }
            try { Clipboard.SetText(nip); BtnKopiujNip.Content = "✓ skopiowano"; }
            catch { }
            // przywróć etykietę po chwili
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            t.Tick += (s, ev) => { BtnKopiujNip.Content = "📋 NIP"; t.Stop(); };
            t.Start();
        }

        private void BtnZadzwon_Click(object sender, RoutedEventArgs e)
        {
            string tel = (_edytowany?.TelefonKontakt ?? "").Trim();
            if (string.IsNullOrWhiteSpace(tel) && _hdr != null) tel = (_hdr.Telefon ?? "").Trim();
            if (string.IsNullOrWhiteSpace(tel))
            {
                MessageBox.Show(this, "Brak numeru telefonu. Uzupełnij w zakładce ✏️ Dane.", "Telefon", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"tel:{tel}") { UseShellExecute = true }); }
            catch { Clipboard.SetText(tel); MessageBox.Show(this, "Numer skopiowany do schowka: " + tel, "Telefon", MessageBoxButton.OK, MessageBoxImage.Information); }
        }

        private void BtnEmail_Click(object sender, RoutedEventArgs e)
        {
            string mail = (_edytowany?.EmailKontakt ?? "").Trim();
            if (string.IsNullOrWhiteSpace(mail) && _hdr != null) mail = (_hdr.Email ?? "").Trim();
            if (string.IsNullOrWhiteSpace(mail))
            {
                MessageBox.Show(this, "Brak adresu email. Uzupełnij w zakładce ✏️ Dane.", "Email", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"mailto:{mail}") { UseShellExecute = true }); }
            catch { Clipboard.SetText(mail); MessageBox.Show(this, "Email skopiowany do schowka: " + mail, "Email", MessageBoxButton.OK, MessageBoxImage.Information); }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Preload zdjęć towarów w tle
            _ = Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.LoadAsync(ConnLibra);

            if (_selectedKlientId.HasValue)
            {
                await LoadKlientAsync(_selectedKlientId.Value);
            }
            else
            {
                // Auto-otwórz picker — user nie musi szukać przycisku
                await Dispatcher.BeginInvoke(new Action(() => BtnPickKlient_Click(this, new RoutedEventArgs())));
            }
        }

        // ── Picker klienta — pełnoekranowy dialog ──
        private async void BtnPickKlient_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new KlientPickerDialog { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Selected != null)
            {
                LblPickKlient.Text = dlg.Selected.Nazwa;
                _selectedKlientId = dlg.Selected.Id;
                await LoadKlientAsync(dlg.Selected.Id);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedKlientId.HasValue) await LoadKlientAsync(_selectedKlientId.Value);
        }

        private void BtnDebug_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedKlientId.HasValue)
            {
                MessageBox.Show(this, "Najpierw wybierz klienta.", "Debug", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new Customer360DiagWindow(_selectedKlientId.Value) { Owner = this };
            dlg.Show();
        }

        // ── Główne ładowanie ──
        #endregion

        #region Ładowanie i orkiestracja

        private async Task LoadKlientAsync(int klientId, bool forceScore = false)
        {
            try
            {
                Cursor = System.Windows.Input.Cursors.Wait;
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = "Ładuję dane klienta…";

                // Paralelnie: header + KPI + history + monthly + top towary + faktury detail + weryfikacja + anulowane
                // Okres wg selektora (_okres): 0 = cała historia, inaczej N miesięcy wstecz
                int OKRES = _okres;
                var tHdr = _service.GetKlientHeaderAsync(klientId);
                var tKpi = _service.GetKpiAsync(klientId, forceScore);
                var tHist = _service.GetOrderHistoryAsync(klientId, OKRES);
                // Wykres obrotu miesięcznego = FAKTURY (pełna historia). Zamówienia mają pozycje dopiero od ~10/2025.
                var tMonthly = _service.GetMonthlyObrotFakturyAsync(klientId, OKRES);
                var tTop = _service.GetTopTowaryAsync(klientId, OKRES, 5);
                var tFakDet = _service.GetFakturyDetailAsync(klientId, OKRES);
                var tWer = _service.GetWeryfikacjaAsync(klientId, OKRES);
                var tAnul = _service.GetAnulowaneZamowieniaAsync(klientId, OKRES);
                var tPorownanie = _service.GetPorownanieMiesiaceAsync(klientId, OKRES);

                await Task.WhenAll(tHdr, tKpi, tHist, tMonthly, tTop, tFakDet, tWer, tAnul, tPorownanie);

                var hdr = await tHdr;
                var kpi = await tKpi;
                var history = await tHist;
                var monthly = await tMonthly;
                var topT = await tTop;
                var fakturyDet = await tFakDet;
                var (werSumma, werTowary) = await tWer;
                var anulowane = await tAnul;
                var scoring = kpi.Score;   // scoring liczony razem z KPI (4 składniki z configu)
                var porownanie = await tPorownanie;

                if (hdr == null)
                {
                    MessageBox.Show(this, "Nie znaleziono klienta.", "Brak", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                _hdr = hdr;
                _fakturyDet = fakturyDet;
                _history = history;

                // Migawka do eksportu PDF
                _snapshot = new Services.Customer360Snapshot
                {
                    KlientId = klientId,
                    Nazwa = hdr.Nazwa,
                    NIP = hdr.NIP,
                    Adres = hdr.AdresPelny,
                    Handlowiec = hdr.Handlowiec,
                    Kpi = kpi,
                    Score = kpi.Score,
                    Obrot = monthly,
                    TopTowary = topT,
                    Alerty = BudujListeAlertow(kpi, werSumma)
                };

                // ── KROK 1: NAJPIERW bind gridów + baner (to czego user potrzebuje) ──
                // Robimy to PRZED renderami, żeby ewentualny wyjątek w wykresie/KPI
                // NIE zablokował wyświetlenia tabel.
                GridZamowienia.ItemsSource = history;
                GridFakturyDetail.ItemsSource = fakturyDet;
                GridAnulowane.ItemsSource = anulowane;
                _werTowary = werTowary;

                // Baner diagnostyczny — pełny zakres + rozbicie po latach
                if (fakturyDet.Count == 0)
                {
                    FakturyDiag.Text = "⚠ Brak faktur dla tego klienta w HANDEL (sprawdź czy khid = KlientId).";
                }
                else
                {
                    var min = fakturyDet.Min(f => f.DataWystawienia);
                    var max = fakturyDet.Max(f => f.DataWystawienia);
                    var lata = fakturyDet.GroupBy(f => f.DataWystawienia.Year).OrderBy(g => g.Key)
                                         .Select(g => $"{g.Key}:{g.Count()}");
                    FakturyDiag.Text = $"📅 {fakturyDet.Count} faktur+korekt · {min:dd.MM.yyyy} – {max:dd.MM.yyyy} · [{string.Join("  ", lata)}]";
                }

                // ── KROK 2: rendery — każdy w osobnym try/catch (jeden błąd nie psuje reszty) ──
                try { RenderHeader(hdr, kpi); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 RenderHeader] " + ex.Message); }
                try { RenderKpi(kpi); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 RenderKpi] " + ex.Message); }
                try { RenderScoring(scoring); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 RenderScoring] " + ex.Message); }
                try { RenderMonthlyChart(monthly); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 RenderMonthlyChart] " + ex.Message); }
                try { RenderWeryfikacja(werSumma, werTowary); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 RenderWer] " + ex.Message); }
                try { RenderPorownanieChart(porownanie); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 RenderPorownanie] " + ex.Message); }
                try { RenderAnulowaneHeader(anulowane); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 RenderAnul] " + ex.Message); }
                try { RenderAlerty(kpi, werSumma, scoring); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 RenderAlerty] " + ex.Message); }
                try { RenderScoringDetal(scoring, kpi); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 RenderScoringDetal] " + ex.Message); }

                // Klient (Dane+Kontakty) — eager (lekkie, potrzebne do szybkich akcji telefon/email)
                try { await LoadKlientTabAsync(klientId, hdr); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 KlientTab] " + ex.Message); }

                // Analiza (Historia+Transport+Asortyment) — LAZY, ładowana przy wejściu w zakładkę Analiza
                _analizaTabLoaded = false;
                if (MainTabs.SelectedIndex == 3)   // jeśli nawigujemy będąc na Analizie — załaduj od razu
                    await ZaladujGrupeAsync(3);

                // Zdjęcia towarów — best-effort, na końcu
                try
                {
                    await Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.LoadAsync(ConnLibra);
                    foreach (var t in topT)
                        t.Image = Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.Get(t.KodTowaru);
                    GridTopTowary.ItemsSource = topT;
                }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 zdjecia] " + ex.Message); }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Błąd ładowania: " + ex.Message + "\n\n" + ex.StackTrace, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Cursor = System.Windows.Input.Cursors.Arrow;
                LoadingOverlay.Visibility = Visibility.Collapsed;
                if (_hdr != null) Title = $"👤 {_hdr.Nazwa} — Karta Klienta 360°";
                OdswiezNawigacje();
                ZapiszOstatnioOtwarty();
            }
        }

        #endregion

        #region Render — Przegląd (nagłówek, KPI, porównanie, scoring, alerty)

        private void RenderHeader(KlientHeader hdr, KlientKpi kpi)
        {
            HeaderEmpty.Visibility = Visibility.Collapsed;
            HeaderLoaded.Visibility = Visibility.Visible;

            LblNazwa.Text = hdr.Nazwa;
            LblNip.Text = string.IsNullOrWhiteSpace(hdr.NIP) ? "—" : hdr.NIP;
            LblAdres.Text = string.IsNullOrWhiteSpace(hdr.AdresPelny) ? "—" : hdr.AdresPelny;
            LblTelefon.Text = string.IsNullOrWhiteSpace(hdr.Telefon) ? "—" : hdr.Telefon;
            LblEmail.Text = string.IsNullOrWhiteSpace(hdr.Email) ? "—" : hdr.Email;
            LblHandlowiec.Text = string.IsNullOrWhiteSpace(hdr.Handlowiec) ? "(brak)" : hdr.Handlowiec;

            if (!string.IsNullOrWhiteSpace(hdr.Kategoria))
            {
                LblKategoria.Text = "Kat. " + hdr.Kategoria.ToUpper();
                ChipKategoria.Visibility = Visibility.Visible;
                (string bg, string brd, string fg) = hdr.Kategoria.ToUpper() switch
                {
                    "A" => ("#DCFCE7", "#86EFAC", "#15803D"),
                    "B" => ("#DBEAFE", "#93C5FD", "#1E40AF"),
                    "C" => ("#FEF3C7", "#FCD34D", "#92400E"),
                    "D" => ("#FEE2E2", "#FCA5A5", "#991B1B"),
                    _ => ("#F1F5F9", "#CBD5E1", "#475569")
                };
                ChipKategoria.Background = B(bg);
                ChipKategoria.BorderBrush = B(brd);
                LblKategoria.Foreground = B(fg);
            }

            // Churn risk badge
            ChipChurn.Visibility = Visibility.Visible;
            (string icon, string text, string churnBg, string churnBrd, string churnFg) = kpi.ChurnRiskLevel switch
            {
                "OK" => ("✅", "Aktywny", "#DCFCE7", "#86EFAC", "#15803D"),
                "WATCH" => ("👀", "Obserwuj", "#FEF3C7", "#FCD34D", "#92400E"),
                "WARNING" => ("⚠", "Uwaga", "#FED7AA", "#FB923C", "#9A3412"),
                "CRITICAL" => ("🚨", "Krytyczne", "#FEE2E2", "#FCA5A5", "#991B1B"),
                _ => ("❓", "Brak danych", "#F1F5F9", "#CBD5E1", "#475569")
            };
            LblChurnIcon.Text = icon;
            LblChurnLevel.Text = text;
            ChipChurn.Background = B(churnBg);
            ChipChurn.BorderBrush = B(churnBrd);
            LblChurnLevel.Foreground = B(churnFg);
            ChipChurn.ToolTip = kpi.ChurnRiskReason;
        }

        private void RenderKpi(KlientKpi kpi)
        {

            // ── KPI finansowe ──
            KpiObrot.Text = $"{kpi.Obrot12M:N0} zł";
            if (kpi.Obrot12MPrev > 0)
            {
                decimal yoy = (kpi.Obrot12M - kpi.Obrot12MPrev) / kpi.Obrot12MPrev * 100m;
                string arrow = yoy >= 0 ? "▲" : "▼";
                KpiObrotYoY.Text = $"{arrow} {Math.Abs(yoy):N1}% YoY";
                KpiObrotYoY.Foreground = B(yoy >= 0 ? "#16A34A" : "#DC2626");
            }
            else KpiObrotYoY.Text = "Brak danych YoY";

            // Śr. wartość faktury = obrót 12M / liczba faktur 12M (z faktur — wiarygodne, zastąpiło zmyśloną marżę)
            decimal srFaktura = kpi.LiczbaFaktur12M > 0 ? kpi.Obrot12M / kpi.LiczbaFaktur12M : 0m;
            KpiSrFaktura.Text = $"{srFaktura:N0} zł";
            KpiSrFakturaSub.Text = $"z {kpi.LiczbaFaktur12M} faktur (12M)";

            KpiLiczbaZam.Text = kpi.LiczbaZamowien12M.ToString("N0");
            KpiSumaKg.Text = $"{kpi.SumaKg12M:N0} kg łącznie";

            KpiLimit.Text = $"{kpi.LimitKredytowy:N0} zł";
            KpiDoZap.Text = $"Do zapłaty: {kpi.DoZaplaty:N0} zł · {kpi.LiczbaFaktur} fakt.";

            // ── Chip: wykorzystanie limitu (kolor wg progu) ──
            decimal wyk = kpi.WykorzystanieLimitProc;
            ChipLimitVal.Text = kpi.LimitKredytowy > 0 ? $"{wyk:N0}%" : "brak limitu";
            ChipLimitBar.Value = (double)Math.Min(100, Math.Max(0, wyk));
            string limitKolor = wyk < 50 ? "#16A34A" : wyk < 80 ? "#F59E0B" : "#DC2626";
            ChipLimitBar.Foreground = B(limitKolor);
            ChipLimitVal.Foreground = B(kpi.LimitKredytowy > 0 ? limitKolor : "#94A3B8");

            // ── Chip: przeterminowane ──
            ChipPrzetermVal.Text = $"{kpi.Przeterminowane:N0} zł";
            ChipPrzetermVal.Foreground = B(kpi.Przeterminowane > 0.01m ? "#DC2626" : "#16A34A");
            ChipPrzetermSub.Text = kpi.MaxDniOpoznienia > 0 ? $"Max {kpi.MaxDniOpoznienia} dni opóźnienia" : "Wszystko w terminie ✓";

            // ── Chip: od ostatniego zamówienia ──
            if (kpi.OstatnieZamowienie.HasValue)
            {
                int dni = kpi.DniOdOstatniegoZamowienia;
                ChipOstatnieVal.Text = $"{dni} dni";
                ChipOstatnieSub.Text = $"Norma: co {kpi.SredniCzasMiedzyZamowieniami:N0} dni";
                // czerwony jeśli przekracza 2× normę
                bool spozniony = kpi.SredniCzasMiedzyZamowieniami > 0 && dni > kpi.SredniCzasMiedzyZamowieniami * 2;
                ChipOstatnieVal.Foreground = B(spozniony ? "#DC2626" : "#0F172A");
            }
            else { ChipOstatnieVal.Text = "Brak"; ChipOstatnieSub.Text = "—"; }

            // ── Chip: reklamacje ──
            KpiReklamacje.Text = kpi.LiczbaReklamacji12M.ToString();
            KpiReklamacjeProc.Text = kpi.LiczbaReklamacji12M > 0 ? $"{kpi.RelativeReklamacjeProc:N2}% obrotu" : "Brak reklamacji ✓";

            // ── Churn (hero) ──
            string churnBg = kpi.ChurnRiskLevel switch
            {
                "CRITICAL" => "#DC2626", "WARNING" => "#F59E0B", "WATCH" => "#3B82F6", "OK" => "#16A34A", _ => "#6B7280"
            };
            string churnLabel = kpi.ChurnRiskLevel switch
            {
                "CRITICAL" => "🔴 KRYTYCZNE", "WARNING" => "🟠 OSTRZEŻENIE", "WATCH" => "🔵 OBSERWUJ", "OK" => "🟢 OK", _ => "— nieznane"
            };
            ChurnText.Text = churnLabel;
            ChurnBadge.Background = B(churnBg);
            ChurnReason.Text = kpi.ChurnRiskReason;
        }

        // ── Wykres niedotrzymania: zamówione vs zafakturowane kg / miesiąc ──
        private void RenderPorownanieChart(List<Models.PorownanieMiesiac> data)
        {
            ChartPorownanie.Children.Clear();
            ChartPorownanie.ColumnDefinitions.Clear();

            if (data == null || data.Count == 0)
            {
                PorownanieInfo.Text = "Brak danych do porównania (pozycje zamówień zapisywane od ~10/2025).";
                ChartPorownanie.Children.Add(new TextBlock { Text = "Brak danych", Foreground = B("#94A3B8"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                return;
            }

            decimal max = Math.Max(data.Max(d => d.ZamowioneKg), data.Max(d => d.ZafakturowaneKg));
            if (max == 0) max = 1;
            const double PLOT_H = 175;

            // Podsumowanie niedotrzymania (tylko miesiące gdzie były zamówienia)
            var zMies = data.Where(d => d.ZamowioneKg > 0).ToList();
            decimal sumZam = zMies.Sum(d => d.ZamowioneKg);
            decimal sumFak = zMies.Sum(d => d.ZafakturowaneKg);
            decimal real = sumZam > 0 ? sumFak / sumZam * 100m : 0m;
            PorownanieInfo.Text = zMies.Count > 0
                ? $"Realizacja zamówień: {real:N1}% · zamówione {sumZam:N0} kg → zafakturowane {sumFak:N0} kg · różnica {(sumFak - sumZam):+#,##0;-#,##0;0} kg ({zMies.Count} mies z zamówieniami)"
                : "Brak miesięcy z pozycjami zamówień (dane od ~10/2025).";

            var wrap = new Grid();
            wrap.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // procenty realizacji
            wrap.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });  // slupki
            wrap.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });                       // nazwy miesiecy

            // procenty nad para slupkow — kolor zielony >=95%, zolty >=80%, czerwony nizej
            var procLabels = new Grid();
            for (int i = 0; i < data.Count; i++) procLabels.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < data.Count; i++)
            {
                var d = data[i];
                if (d.ZamowioneKg <= 0) continue;
                decimal p = d.RealizacjaProc;
                string kolor = p >= 95m ? "#16A34A" : p >= 80m ? "#EAB308" : "#DC2626";
                var tb = new TextBlock
                {
                    Text = $"{p:N0}%",
                    FontSize = 10, FontWeight = FontWeights.SemiBold,
                    Foreground = B(kolor),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                Grid.SetColumn(tb, i);
                procLabels.Children.Add(tb);
            }
            Grid.SetRow(procLabels, 0);
            wrap.Children.Add(procLabels);

            var plot = new Grid();
            for (int i = 0; i < data.Count; i++) plot.ColumnDefinitions.Add(new ColumnDefinition());

            for (int i = 0; i < data.Count; i++)
            {
                var d = data[i];
                string nazwaMies = (d.Month >= 1 && d.Month <= 12) ? MiesPelny[d.Month - 1] : d.Month.ToString();
                double hZam = (double)(d.ZamowioneKg / max) * PLOT_H;
                double hFak = (double)(d.ZafakturowaneKg / max) * PLOT_H;

                // para słupków obok siebie
                var para = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(2, 0, 2, 0) };

                // kolor zafakturowanego: zielony gdy ≥ zamówione, czerwony gdy mocno mniej (niedotrzymane)
                string fakKolor = "#16A34A";
                if (d.ZamowioneKg > 0 && d.ZafakturowaneKg < d.ZamowioneKg * 0.95m) fakKolor = "#DC2626";

                string tip = $"{nazwaMies} {d.Year}\nZamówione: {d.ZamowioneKg:N0} kg\nZafakturowane: {d.ZafakturowaneKg:N0} kg\nRóżnica: {d.RoznicaKg:+#,##0;-#,##0;0} kg"
                             + (d.ZamowioneKg > 0 ? $"\nRealizacja: {d.RealizacjaProc:N0}%" : "");

                var bZam = new Border { Width = 16, Height = Math.Max(2, hZam), CornerRadius = new CornerRadius(3, 3, 0, 0), Background = B("#3B82F6"), Margin = new Thickness(0, 0, 2, 0), VerticalAlignment = VerticalAlignment.Bottom, ToolTip = tip };
                var bFak = new Border { Width = 16, Height = Math.Max(2, hFak), CornerRadius = new CornerRadius(3, 3, 0, 0), Background = B(fakKolor), VerticalAlignment = VerticalAlignment.Bottom, ToolTip = tip, Cursor = System.Windows.Input.Cursors.Hand };
                int rok = d.Year, mc = d.Month;
                bFak.MouseLeftButtonDown += (s, ev) => PokazSzczegolyMiesiaca(rok, mc);
                bZam.MouseLeftButtonDown += (s, ev) => PokazSzczegolyMiesiaca(rok, mc);
                para.Children.Add(bZam);
                para.Children.Add(bFak);

                Grid.SetColumn(para, i);
                plot.Children.Add(para);
            }
            Grid.SetRow(plot, 1);
            wrap.Children.Add(plot);

            var labels = new Grid();
            for (int i = 0; i < data.Count; i++) labels.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < data.Count; i++)
            {
                var d = data[i];
                bool nowyRok = i == 0 || data[i - 1].Year != d.Year;
                string skrot = (d.Month >= 1 && d.Month <= 12) ? MiesSkrot[d.Month - 1] : d.Month.ToString();
                var tb = new TextBlock
                {
                    Text = nowyRok ? $"{skrot}\n{d.Year}" : skrot,
                    FontSize = 9, Foreground = B(nowyRok ? "#1E40AF" : "#94A3B8"),
                    FontWeight = nowyRok ? FontWeights.Bold : FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0)
                };
                Grid.SetColumn(tb, i);
                labels.Children.Add(tb);
            }
            Grid.SetRow(labels, 2);
            wrap.Children.Add(labels);

            ChartPorownanie.Children.Add(wrap);
        }

        // Drill-down: kliknięcie słupka wykresu → faktury + zamówienia danego miesiąca
        private void PokazSzczegolyMiesiaca(int rok, int miesiac)
        {
            var faktury = _fakturyDet
                .Where(f => f.DataWystawienia.Year == rok && f.DataWystawienia.Month == miesiac)
                .OrderBy(f => f.DataWystawienia).ToList();
            var zamowienia = _history
                .Where(z => z.DataZamowienia.Year == rok && z.DataZamowienia.Month == miesiac)
                .OrderBy(z => z.DataZamowienia).ToList();

            string nazwaMies = (miesiac >= 1 && miesiac <= 12) ? MiesPelny[miesiac - 1] : miesiac.ToString();
            string tytul = $"📅 {nazwaMies} {rok} — {(_hdr?.Nazwa ?? "")}";
            var dlg = new SzczegolyMiesiacaDialog(tytul, faktury, zamowienia) { Owner = this };
            dlg.ShowDialog();
        }

        // ── Render scoringu 4-składnikowego (hero) ──
        private void RenderScoring(Customer360Score? sc)
        {
            ScoringBary.Children.Clear();

            if (sc == null)
            {
                ScoringLitera.Text = "?";
                ScoringPunkty.Text = "—/100";
                ScoringKategoria.Text = "Scoring niedostępny";
                ScoringRekomendacja.Text = "—";
                ChipScoring.Visibility = Visibility.Collapsed;
                return;
            }

            ScoringLitera.Text = sc.Litera;
            ScoringPunkty.Text = $"{sc.Total}/100";
            ScoringKategoria.Text = $"Ocena: {sc.Kategoria} ({sc.Total} pkt)";
            ScoringBadge.Background = B(sc.KategoriaKolor);

            // Badge w nagłówku (zawsze widoczny)
            LblScoringChip.Text = $"⭐ {sc.Litera} · {sc.Total}";
            ChipScoring.Background = B(sc.KategoriaKolor);
            ChipScoring.Visibility = Visibility.Visible;
            ScoringRekomendacja.Text = sc.RekomendacjaLimitu > 0
                ? $"{sc.RekomendacjaLimitu:N0} zł"
                : "0 zł (wstrzymać kredyt)";

            // 4 paski składników (każdy 0-100, etykieta z wagą)
            AddScoringBar($"Obrót 12M ({sc.WagaObrot}%)", sc.ObrotPkt, 100, "#2563EB", B);
            AddScoringBar($"Częstotliwość zamówień ({sc.WagaCzestotliwosc}%)", sc.CzestotliwoscPkt, 100, "#10B981", B);
            AddScoringBar($"Terminowość płatności ({sc.WagaTerminowosc}%)", sc.TerminowoscPkt, 100, "#3B82F6", B);
            AddScoringBar($"Długość relacji ({sc.WagaDlugosc}%)", sc.DlugoscPkt, 100, "#8B5CF6", B);
        }

        private void AddScoringBar(string label, int pkt, int max, string kolor, Func<string, Brush> B)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(210) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(48) });

            var lbl = new TextBlock { Text = label, FontSize = 11, Foreground = B("#475569"), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0);
            grid.Children.Add(lbl);

            var track = new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = B("#E2E8F0"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0) };
            var fillGrid = new Grid();
            fillGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(max > 0 ? pkt : 0, GridUnitType.Star) });
            fillGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(max > 0 ? (max - pkt) : 1, GridUnitType.Star) });
            var fill = new Border { CornerRadius = new CornerRadius(5), Background = B(kolor) };
            Grid.SetColumn(fill, 0);
            fillGrid.Children.Add(fill);
            track.Child = fillGrid;
            Grid.SetColumn(track, 1);
            grid.Children.Add(track);

            var val = new TextBlock { Text = $"{pkt}/{max}", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = B("#0F172A"), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(val, 2);
            grid.Children.Add(val);

            ScoringBary.Children.Add(grid);
        }

        // ── Render panelu alertów (prawa kolumna) ──
        private void RenderAlerty(KlientKpi kpi, Kalendarz1.Customer360.Models.WeryfikacjaSumarum wer, Customer360Score? sc)
        {
            AlertyPanel.Children.Clear();

            void Alert(string ikona, string tekst, string kolor)
            {
                var border = new Border { Background = B(kolor + "14"), BorderBrush = B(kolor), BorderThickness = new Thickness(0, 0, 0, 0), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 7, 10, 7), Margin = new Thickness(0, 0, 0, 6) };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock { Text = ikona, FontSize = 14, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Top });
                sp.Children.Add(new TextBlock { Text = tekst, FontSize = 11, Foreground = B("#0F172A"), TextWrapping = TextWrapping.Wrap, MaxWidth = 240, VerticalAlignment = VerticalAlignment.Center });
                border.Child = sp;
                AlertyPanel.Children.Add(border);
            }

            // Przeterminowane
            if (kpi.Przeterminowane > 0.01m)
                Alert("🔴", $"Przeterminowane {kpi.Przeterminowane:N0} zł (max {kpi.MaxDniOpoznienia} dni po terminie)", "#DC2626");
            // Limit
            if (kpi.LimitKredytowy > 0 && kpi.WykorzystanieLimitProc >= 80)
                Alert("⚠", $"Limit kredytowy wykorzystany w {kpi.WykorzystanieLimitProc:N0}%", "#F59E0B");
            // Churn
            if (kpi.ChurnRiskLevel == "CRITICAL" || kpi.ChurnRiskLevel == "WARNING")
                Alert("📉", $"Ryzyko odejścia: {kpi.ChurnRiskReason}", "#F59E0B");
            // Ostatnie zamówienie
            if (kpi.OstatnieZamowienie.HasValue && kpi.SredniCzasMiedzyZamowieniami > 0
                && kpi.DniOdOstatniegoZamowienia > kpi.SredniCzasMiedzyZamowieniami * 2)
                Alert("⏰", $"{kpi.DniOdOstatniegoZamowienia} dni bez zamówienia (norma co {kpi.SredniCzasMiedzyZamowieniami:N0} dni)", "#F59E0B");
            // Weryfikacja — ucięcia
            if (wer.LiczbaTowarowUcietych > 0)
                Alert("✂", $"{wer.LiczbaTowarowUcietych} towarów ucięte (zamówiono więcej niż zafakturowano)", "#3B82F6");
            // Reklamacje
            if (kpi.LiczbaReklamacji12M > 0)
                Alert("📋", $"{kpi.LiczbaReklamacji12M} reklamacji w 12 mies ({kpi.RelativeReklamacjeProc:N1}% obrotu)", "#3B82F6");
            // Scoring rekomendacja
            if (sc != null && !string.IsNullOrWhiteSpace(sc.RekomendacjaOpis))
                Alert("💡", sc.RekomendacjaOpis, "#1E40AF");

            // Jeśli brak alertów — pozytywny komunikat
            if (AlertyPanel.Children.Count == 0)
                Alert("✅", "Brak sygnałów ostrzegawczych — klient w dobrej kondycji.", "#16A34A");
        }

        // ════════════════════════════════════════════════════════════════════
        // ZAKŁADKI Z KARTOTEKI (lazy-load):
        //   Klient  = Dane/edycja + Kontakty   → LoadKlientTabAsync
        //   Analiza = Historia + Transport + Asortyment → LoadAnalizaTabAsync
        // ════════════════════════════════════════════════════════════════════

        #endregion

        #region Zakładki Klient / Analiza (dane, kontakty, transport, asortyment)

        private async Task LoadKlientTabAsync(int klientId, KlientHeader hdr)
        {
            // Dane podstawowe (read-only) z headera
            try
            {
                EdNazwa.Text = hdr.Nazwa;
                EdNip.Text = string.IsNullOrWhiteSpace(hdr.NIP) ? "—" : hdr.NIP;
                EdAdres.Text = string.IsNullOrWhiteSpace(hdr.AdresPelny) ? "—" : hdr.AdresPelny;
            }
            catch { }

            // Dane własne (edycja) — reużycie WczytajDaneWlasneAsync na 1-elementowej liście
            try
            {
                _edytowany = new Kalendarz1.Kartoteka.Models.OdbiorcaHandlowca { IdSymfonia = klientId };
                await _kartoteka.WczytajDaneWlasneAsync(new List<Kalendarz1.Kartoteka.Models.OdbiorcaHandlowca> { _edytowany });
                EdOsoba.Text = _edytowany.OsobaKontaktowa ?? "";
                EdTelefon.Text = _edytowany.TelefonKontakt ?? "";
                EdEmail.Text = _edytowany.EmailKontakt ?? "";
                EdTrasa.Text = _edytowany.Trasa ?? "";
                EdDzien.Text = _edytowany.PreferowanyDzienDostawy ?? "";
                EdGodzina.Text = _edytowany.PreferowanaGodzinaDostawy ?? "";
                EdAdresDostawy.Text = _edytowany.AdresDostawyInny ?? "";
                EdPrefPakowanie.Text = _edytowany.PreferencjePakowania ?? "";
                EdPrefJakosc.Text = _edytowany.PreferencjeJakosci ?? "";
                EdNotatki.Text = _edytowany.Notatki ?? "";
                string kat = (_edytowany.KategoriaHandlowca ?? "C").Trim().ToUpper();
                EdKategoria.SelectedIndex = kat switch { "A" => 0, "B" => 1, "C" => 2, "D" => 3, _ => 2 };
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 dane wlasne] " + ex.Message); }

            // Kontakty
            try
            {
                var kontakty = await _kartoteka.PobierzKontaktyAsync(klientId);
                GridKontakty.ItemsSource = kontakty;
                EmptyKontakty.Visibility = kontakty.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 kontakty] " + ex.Message); }
        }

        private async Task LoadAnalizaTabAsync(int klientId)
        {
            // Historia zmian
            try
            {
                var hist = await _historia.PobierzHistorieKlientaAsync(klientId);
                GridHistoria.ItemsSource = hist;
                EmptyHistoria.Visibility = hist.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 historia] " + ex.Message); }

            // Transport
            try { RenderTransport(await _kartoteka.PobierzTransportAnalizaAsync(klientId, 12)); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 transport] " + ex.Message); }

            // Asortyment szczegółowy (cała historia)
            try
            {
                var asort = await _kartoteka.PobierzAsortymentSzczegolyAsync(klientId, 120);
                GridAsortyment.ItemsSource = asort;
                EmptyAsortyment.Visibility = asort.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                RenderAsortymentUdzial(asort);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("[C360 asortyment] " + ex.Message); }
        }

        // ── Struktura asortymentu: poziome paski udziału w obrocie (top 8) ──
        private void RenderAsortymentUdzial(List<Kalendarz1.Kartoteka.Models.AsortymentPozycja> asort)
        {
            if (asort == null || asort.Count == 0)
            {
                ChartAsortyment.Series = System.Array.Empty<ISeries>();
                return;
            }

            decimal suma = asort.Sum(a => a.SumaWartosc);
            if (suma <= 0) suma = 1;
            var top = asort.OrderByDescending(a => a.SumaWartosc).Take(8).ToList();
            decimal inne = suma - top.Sum(a => a.SumaWartosc);

            var serie = new List<ISeries>();

            PieSeries<double> Kawalek(string nazwa, decimal wartosc, int kolorIdx)
            {
                decimal proc = wartosc / suma * 100m;
                string nazwaSkr = nazwa.Length > 28 ? nazwa.Substring(0, 27) + "…" : nazwa;
                return new PieSeries<double>
                {
                    Values = new[] { (double)wartosc },
                    Name = $"{nazwaSkr} ({proc:N0}%)",
                    Fill = new SolidColorPaint(Customer360Palette.Color(kolorIdx)),
                    DataLabelsPaint = new SolidColorPaint(SkiaSharp.SKColors.White),
                    DataLabelsSize = 12,
                    DataLabelsPosition = LiveChartsCore.Measure.PolarLabelsPosition.Middle,
                    DataLabelsFormatter = _ => proc >= 4 ? $"{proc:N0}%" : "",   // ukryj etykietę dla bardzo małych kawałków
                    ToolTipLabelFormatter = _ => $"{nazwa}: {wartosc:N0} zł ({proc:N1}%)"
                };
            }

            for (int i = 0; i < top.Count; i++)
            {
                var a = top[i];
                string nazwa = string.IsNullOrWhiteSpace(a.ProduktNazwa) ? a.ProduktKod : a.ProduktNazwa;
                serie.Add(Kawalek(nazwa, a.SumaWartosc, i));
            }
            if (inne > 0.01m) serie.Add(Kawalek("Pozostałe", inne, 7));

            ChartAsortyment.Series = serie;
        }

        private void RenderTransport(Kalendarz1.Kartoteka.Models.TransportAnaliza t)
        {
            TransportKierowcy.Children.Clear();
            TransportPojazdy.Children.Clear();
            TransportTrasy.Children.Clear();
            TransportHeader.Text = $"🚚 Analiza transportu — {t.LiczbaKursow} kursów (ostatnie 12 mies)";

            void Row(StackPanel host, string glowny, string detal, int kursy)
            {
                var b = new Border { Background = B("#F8FAFC"), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 5) };
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = glowny, FontWeight = FontWeights.SemiBold, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
                if (!string.IsNullOrWhiteSpace(detal)) sp.Children.Add(new TextBlock { Text = detal, FontSize = 10, Foreground = B("#64748B") });
                sp.Children.Add(new TextBlock { Text = $"{kursy} kursów", FontSize = 10, Foreground = B("#2563EB"), FontWeight = FontWeights.SemiBold });
                b.Child = sp;
                host.Children.Add(b);
            }

            if (t.Kierowcy.Count == 0) TransportKierowcy.Children.Add(new TextBlock { Text = "Brak danych", FontSize = 11, Foreground = Brushes.Gray });
            foreach (var k in t.Kierowcy) Row(TransportKierowcy, k.Nazwa ?? "—", k.Telefon, k.LiczbaKursow);

            if (t.Pojazdy.Count == 0) TransportPojazdy.Children.Add(new TextBlock { Text = "Brak danych", FontSize = 11, Foreground = Brushes.Gray });
            foreach (var p in t.Pojazdy) Row(TransportPojazdy, p.Nazwa ?? "—", p.PaletyH1 > 0 ? $"{p.PaletyH1} palet" : "", p.LiczbaKursow);

            if (t.Trasy.Count == 0) TransportTrasy.Children.Add(new TextBlock { Text = "Brak danych", FontSize = 11, Foreground = Brushes.Gray });
            foreach (var tr in t.Trasy) Row(TransportTrasy, tr.Nazwa ?? "—", "", tr.LiczbaKursow);
        }

        // ── Scoring — szczegółowa zakładka ──
        // sc = punkty/wagi/litera (z cache, do 7 dni temu)
        // kpi = aktualne KPI (zawsze swieze) — uzywane do opisow zeby zgadzaly sie z hero KPI tile
        private void RenderScoringDetal(Customer360Score? sc, KlientKpi? kpi)
        {
            ScoringDetalPanel.Children.Clear();

            if (sc == null)
            {
                ScoringDetalPanel.Children.Add(new TextBlock { Text = "Scoring niedostępny dla tego klienta.", Foreground = Brushes.Gray });
                return;
            }

            // Nagłówek z oceną + przycisk ustawień
            var head = new Border { Style = (Style)FindResource("CardStyle"), Margin = new Thickness(0, 0, 0, 12) };
            var headGrid = new Grid();
            headGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });
            var hsp = new StackPanel { Orientation = Orientation.Horizontal };
            var circle = new Border { Width = 80, Height = 80, CornerRadius = new CornerRadius(40), Background = B(sc.KategoriaKolor), Margin = new Thickness(0, 0, 16, 0) };
            circle.Child = new TextBlock { Text = sc.Litera, FontSize = 34, FontWeight = FontWeights.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            hsp.Children.Add(circle);
            var hinfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            hinfo.Children.Add(new TextBlock { Text = $"{sc.Kategoria} — {sc.Total}/100 pkt", FontSize = 18, FontWeight = FontWeights.Bold });
            hinfo.Children.Add(new TextBlock { Text = sc.RekomendacjaLimitu > 0 ? $"💡 Rekomendowany limit kredytowy: {sc.RekomendacjaLimitu:N0} zł" : "💡 Rekomendacja: wstrzymać kredyt kupiecki", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = B("#1E40AF"), Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrWhiteSpace(sc.RekomendacjaOpis))
                hinfo.Children.Add(new TextBlock { Text = sc.RekomendacjaOpis, FontSize = 11, Foreground = B("#475569"), Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 560 });
            hsp.Children.Add(hinfo);
            Grid.SetColumn(hsp, 0); headGrid.Children.Add(hsp);
            var btnCfg = new Button { Content = "⚙ Ustaw scoring", Padding = new Thickness(12, 7, 12, 7), Cursor = System.Windows.Input.Cursors.Hand, VerticalAlignment = VerticalAlignment.Top, Background = B("#F1F5F9"), BorderThickness = new Thickness(0) };
            btnCfg.Click += BtnUstawScoring_Click;
            Grid.SetColumn(btnCfg, 1); headGrid.Children.Add(btnCfg);
            head.Child = headGrid;
            ScoringDetalPanel.Children.Add(head);

            // Składniki (4, każdy 0-100, z wagą i wartością surową)
            var card = new Border { Style = (Style)FindResource("CardStyle") };
            var csp = new StackPanel();
            csp.Children.Add(new TextBlock { Text = "Składniki oceny", FontWeight = FontWeights.SemiBold, FontSize = 13, Margin = new Thickness(0, 0, 0, 12) });
            void Skladnik(string nazwa, int pkt, int waga, string kolor, string opis)
            {
                var g = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                g.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(260) });
                g.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(70) });
                var left = new StackPanel();
                left.Children.Add(new TextBlock { Text = $"{nazwa}  ({waga}%)", FontWeight = FontWeights.SemiBold, FontSize = 12 });
                left.Children.Add(new TextBlock { Text = opis, FontSize = 10, Foreground = B("#94A3B8"), TextWrapping = TextWrapping.Wrap, MaxWidth = 250 });
                Grid.SetColumn(left, 0); g.Children.Add(left);
                var track = new Border { Height = 14, CornerRadius = new CornerRadius(7), Background = B("#E2E8F0"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
                var fg = new Grid();
                fg.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(Math.Max(0, pkt), GridUnitType.Star) });
                fg.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(Math.Max(0, 100 - pkt), GridUnitType.Star) });
                var fill = new Border { CornerRadius = new CornerRadius(7), Background = B(kolor) };
                Grid.SetColumn(fill, 0); fg.Children.Add(fill);
                track.Child = fg; Grid.SetColumn(track, 1); g.Children.Add(track);
                var val = new TextBlock { Text = $"{pkt}/100", FontWeight = FontWeights.Bold, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                Grid.SetColumn(val, 2); g.Children.Add(val);
                csp.Children.Add(g);
            }
            // Opisy: bierzemy AKTUALNE wartosci z kpi (zeby zgadzaly sie z hero KPI tile);
            // punkty/wagi pozostaja z sc (snapshot — sa policzone razem z kategoria).
            decimal obrotOpis = kpi?.Obrot12M ?? sc.Obrot12M;
            decimal odstepOpis = kpi?.SredniCzasMiedzyZamowieniami ?? sc.SrOdstepDni;
            Skladnik("Obrót 12M", sc.ObrotPkt, sc.WagaObrot, "#2563EB", $"{obrotOpis:N0} zł w ostatnich 12 mies (z faktur)");
            Skladnik("Częstotliwość zamówień", sc.CzestotliwoscPkt, sc.WagaCzestotliwosc, "#10B981", $"Średni odstęp: {odstepOpis:N0} dni między zamówieniami");
            Skladnik("Terminowość płatności", sc.TerminowoscPkt, sc.WagaTerminowosc, "#3B82F6", $"{sc.TerminowoscProc:N0}% salda w terminie");
            Skladnik("Długość relacji", sc.DlugoscPkt, sc.WagaDlugosc, "#8B5CF6", $"Współpraca od {sc.LataRelacji:N1} lat");
            card.Child = csp;
            ScoringDetalPanel.Children.Add(card);
        }

        // Otwiera okno ustawień scoringu; po zapisie przelicza i odświeża kartę
        private async void BtnUstawScoring_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Customer360ScoringConfigWindow { Owner = this };
            if (dlg.ShowDialog() == true && _selectedKlientId.HasValue)
            {
                Services.Customer360ScoringConfigStore.InvalidateCache();
                await Services.Customer360ScoringService.WygasCalyCacheAsync();   // zmiana parametrów → wszystkie cache nieaktualne (pamięć+DB)
                await LoadKlientAsync(_selectedKlientId.Value, forceScore: true);
            }
        }

        // ── Zapis danych własnych ──
        private async void BtnZapiszDane_Click(object sender, RoutedEventArgs e)
        {
            if (_edytowany == null) { EdStatus.Text = "Brak klienta"; return; }
            try
            {
                BtnZapiszDane.IsEnabled = false;
                EdStatus.Text = "Zapisuję…";

                // Zbuduj listę zmian do historii (porównaj stare → nowe)
                var zmiany = new List<Kalendarz1.Kartoteka.Models.ZmianaPola>();
                void Diff(string pole, string stara, string nowa)
                {
                    if ((stara ?? "") != (nowa ?? ""))
                        zmiany.Add(new Kalendarz1.Kartoteka.Models.ZmianaPola { NazwaPola = pole, StaraWartosc = stara, NowaWartosc = nowa });
                }
                string nowaKat = (EdKategoria.SelectedIndex switch { 0 => "A", 1 => "B", 2 => "C", 3 => "D", _ => "C" });
                Diff("OsobaKontaktowa", _edytowany.OsobaKontaktowa, EdOsoba.Text);
                Diff("TelefonKontakt", _edytowany.TelefonKontakt, EdTelefon.Text);
                Diff("EmailKontakt", _edytowany.EmailKontakt, EdEmail.Text);
                Diff("KategoriaHandlowca", _edytowany.KategoriaHandlowca, nowaKat);
                Diff("Trasa", _edytowany.Trasa, EdTrasa.Text);
                Diff("PreferowanyDzienDostawy", _edytowany.PreferowanyDzienDostawy, EdDzien.Text);
                Diff("PreferowanaGodzinaDostawy", _edytowany.PreferowanaGodzinaDostawy, EdGodzina.Text);
                Diff("AdresDostawyInny", _edytowany.AdresDostawyInny, EdAdresDostawy.Text);
                Diff("PreferencjePakowania", _edytowany.PreferencjePakowania, EdPrefPakowanie.Text);
                Diff("PreferencjeJakosci", _edytowany.PreferencjeJakosci, EdPrefJakosc.Text);
                Diff("Notatki", _edytowany.Notatki, EdNotatki.Text);

                // Wpisz nowe wartości do obiektu
                _edytowany.OsobaKontaktowa = EdOsoba.Text;
                _edytowany.TelefonKontakt = EdTelefon.Text;
                _edytowany.EmailKontakt = EdEmail.Text;
                _edytowany.KategoriaHandlowca = nowaKat;
                _edytowany.Trasa = EdTrasa.Text;
                _edytowany.PreferowanyDzienDostawy = EdDzien.Text;
                _edytowany.PreferowanaGodzinaDostawy = EdGodzina.Text;
                _edytowany.AdresDostawyInny = EdAdresDostawy.Text;
                _edytowany.PreferencjePakowania = EdPrefPakowanie.Text;
                _edytowany.PreferencjeJakosci = EdPrefJakosc.Text;
                _edytowany.Notatki = EdNotatki.Text;

                string user = App.UserID ?? "?";
                await _kartoteka.ZapiszDaneWlasneAsync(_edytowany, user);

                // Loguj zmiany do historii
                if (zmiany.Count > 0)
                {
                    try { await _historia.LogujZmianyAsync(_edytowany.IdSymfonia, zmiany, user, App.UserFullName ?? user); }
                    catch (Exception exh) { System.Diagnostics.Debug.WriteLine("[C360 log historia] " + exh.Message); }
                    // Odśwież zakładkę historii
                    try { GridHistoria.ItemsSource = await _historia.PobierzHistorieKlientaAsync(_edytowany.IdSymfonia); } catch { }
                }

                EdStatus.Text = $"✅ Zapisano ({zmiany.Count} zmian) {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                EdStatus.Text = "❌ Błąd zapisu";
                MessageBox.Show(this, "Nie udało się zapisać: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { BtnZapiszDane.IsEnabled = true; }
        }

        // ── Kontakty CRUD ──
        private async void BtnDodajKontakt_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedKlientId.HasValue) return;
            var dlg = new Kalendarz1.Kartoteka.Views.KontaktEdycjaWindow(
                new Kalendarz1.Kartoteka.Models.KontaktOdbiorcy { IdSymfonia = _selectedKlientId.Value }) { Owner = this };
            if (dlg.ShowDialog() == true)
                await OdswiezKontakty();
        }

        private async void BtnEdytujKontakt_Click(object sender, RoutedEventArgs e)
        {
            if (GridKontakty.SelectedItem is not Kalendarz1.Kartoteka.Models.KontaktOdbiorcy k)
            {
                MessageBox.Show(this, "Wybierz kontakt do edycji.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var dlg = new Kalendarz1.Kartoteka.Views.KontaktEdycjaWindow(k) { Owner = this };
            if (dlg.ShowDialog() == true)
                await OdswiezKontakty();
        }

        private async void BtnUsunKontakt_Click(object sender, RoutedEventArgs e)
        {
            if (GridKontakty.SelectedItem is not Kalendarz1.Kartoteka.Models.KontaktOdbiorcy k) return;
            if (MessageBox.Show(this, $"Usunąć kontakt {k.Imie} {k.Nazwisko}?", "Potwierdź", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try { await _kartoteka.UsunKontaktAsync(k.Id); await OdswiezKontakty(); }
            catch (Exception ex) { MessageBox.Show(this, "Błąd usuwania: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async Task OdswiezKontakty()
        {
            if (!_selectedKlientId.HasValue) return;
            try
            {
                var kontakty = await _kartoteka.PobierzKontaktyAsync(_selectedKlientId.Value);
                GridKontakty.ItemsSource = kontakty;
                EmptyKontakty.Visibility = kontakty.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        #endregion

        #region Weryfikacja faktur

        private void RenderWeryfikacja(Kalendarz1.Customer360.Models.WeryfikacjaSumarum s, List<Models.WeryfikacjaTowar> towary)
        {
            _werSummaCache = s;
            _werTowary = towary;

            // ── Werdykt: kółko realizacji + kolor + tekst ──
            decimal real = s.ZgodnoscProc;  // zafakturowane / zamówione * 100
            string kolWerdykt = real >= 98 && real <= 105 ? "#16A34A"     // zielony: pełna realizacja
                              : real >= 90 ? "#F59E0B"                     // żółty: drobne braki
                              : real > 0 ? "#DC2626"                        // czerwony: duże niedotrzymanie
                              : "#6B7280";
            VerProc.Text = s.ZamowioneKg > 0 ? $"{real:N0}%" : "—";
            VerKolo.Background = B(kolWerdykt);

            if (s.ZamowioneKg <= 0 && s.ZafakturowaneKg > 0)
            {
                VerVerdict.Text = "Brak zamówień do porównania (są tylko faktury)";
                VerVerdictSub.Text = "Pozycje zamówień zapisywane od ~10/2025 — wcześniej tylko faktury.";
            }
            else if (real >= 98 && real <= 105)
            {
                VerVerdict.Text = "✅ Zamówienia zafakturowane prawidłowo";
                VerVerdictSub.Text = $"Realizacja {real:N1}% · różnica {s.RoznicaKg:+#,##0;-#,##0;0} kg ({s.RoznicaWartosci:+#,##0;-#,##0;0} zł)";
            }
            else if (s.RoznicaKg < 0)
            {
                VerVerdict.Text = $"⚠ Niedotrzymanie: zafakturowano mniej niż zamówiono";
                VerVerdictSub.Text = $"Brakuje {Math.Abs(s.RoznicaKg):N0} kg ({Math.Abs(s.RoznicaWartosci):N0} zł) · {s.LiczbaTowarowUcietych} towarów uciętych · {s.LiczbaTowarowBrakFaktury} bez faktury";
            }
            else
            {
                VerVerdict.Text = "ℹ Zafakturowano więcej niż zamówiono";
                VerVerdictSub.Text = $"Nadwyżka {s.RoznicaKg:N0} kg ({s.RoznicaWartosci:N0} zł) · {s.LiczbaTowarowDodanych} towarów dodanych poza zamówieniem";
            }

            // Paski zam → fak (proporcja zafakturowane/zamówione)
            decimal bazaKg = Math.Max(s.ZamowioneKg, s.ZafakturowaneKg);
            if (bazaKg <= 0) bazaKg = 1;
            VerZamLbl.Text = $"{s.ZamowioneKg:N0} kg · {s.LiczbaZamowien} zam.";
            VerFakLbl.Text = $"{s.ZafakturowaneKg:N0} kg · {s.LiczbaFaktur} fakt.";
            double fakProc = (double)(s.ZafakturowaneKg / bazaKg);
            VerFakFill.Width = new GridLength(Math.Max(0.01, fakProc), GridUnitType.Star);
            VerFakRest.Width = new GridLength(Math.Max(0.0, 1 - fakProc), GridUnitType.Star);
            VerFakBar.Background = B(kolWerdykt);

            // ── Chipy filtrów ──
            int zgodne = s.LiczbaTowarow - s.LiczbaTowarowUcietych - s.LiczbaTowarowDodanych - s.LiczbaTowarowBrakFaktury;
            VerChips.Children.Clear();
            DodajChip("Wszystkie", s.LiczbaTowarow, "ALL", "#1E40AF", "#EFF6FF");
            DodajChip("✂ Ucięte", s.LiczbaTowarowUcietych, "UCIETE", "#92400E", "#FEF3C7");
            DodajChip("➕ Więcej", s.LiczbaTowarowDodanych, "WIECEJ", "#1E40AF", "#DBEAFE");
            DodajChip("⚠ Brak faktury", s.LiczbaTowarowBrakFaktury, "BRAK", "#991B1B", "#FEE2E2");
            DodajChip("✅ Zgodne", zgodne, "ZGODNE", "#15803D", "#F0FDF4");

            // ── Lista pozycji (z bieżącym filtrem) ──
            RenderWeryfikacjaLista();

            // Banner gdy 0 faktur
            if (s.LiczbaFaktur == 0 && s.LiczbaZamowien > 0)
            {
                VerBannerDiag.Visibility = Visibility.Visible;
                VerDiagTitle.Text = "⚠ Brak faktur sprzedaży w HANDEL dla tego klienta";
                VerDiagText.Text = $"Zamówienia: {s.LiczbaZamowien} ({s.ZamowioneKg:N0} kg), ale brak faktur FVS/FVR/FVZ. Sprawdź czy KlientId = HANDEL.khid.";
            }
            else if (s.LiczbaFaktur == 0 && s.LiczbaZamowien == 0)
            {
                VerBannerDiag.Visibility = Visibility.Visible;
                VerDiagTitle.Text = "ℹ Brak danych w wybranym okresie";
                VerDiagText.Text = "Brak zamówień i faktur. Zmień okres lub sprawdź klienta.";
            }
            else VerBannerDiag.Visibility = Visibility.Collapsed;
        }

        private void DodajChip(string label, int liczba, string filtr, string fg, string bg)
        {
            bool aktywny = _werFiltr == filtr;
            var chip = new Border
            {
                Background = B(aktywny ? fg : bg),
                BorderBrush = B(fg), BorderThickness = new Thickness(aktywny ? 0 : 1),
                CornerRadius = new CornerRadius(16), Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0), Cursor = System.Windows.Input.Cursors.Hand
            };
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = B(aktywny ? "#FFFFFF" : fg), VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(new Border
            {
                Background = B(aktywny ? "#FFFFFF" : fg), CornerRadius = new CornerRadius(9), Margin = new Thickness(6, 0, 0, 0),
                MinWidth = 18, Padding = new Thickness(5, 1, 5, 1),
                Child = new TextBlock { Text = liczba.ToString(), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = B(aktywny ? fg : "#FFFFFF"), HorizontalAlignment = HorizontalAlignment.Center }
            });
            chip.Child = sp;
            chip.MouseLeftButtonDown += (s, e) => { _werFiltr = filtr; RenderWeryfikacja(_werSummaCache!, _werTowary); };
            VerChips.Children.Add(chip);
        }

        private Models.WeryfikacjaSumarum? _werSummaCache;

        private void RenderWeryfikacjaLista()
        {
            WeryfikacjaLista.Children.Clear();

            // Filtruj wg statusu
            IEnumerable<Models.WeryfikacjaTowar> lista = _werTowary;
            lista = _werFiltr switch
            {
                "UCIETE" => lista.Where(t => t.Status.Contains("Ucięte")),
                "WIECEJ" => lista.Where(t => t.Status.Contains("Więcej")),
                "BRAK" => lista.Where(t => t.Status.Contains("Brak faktury")),
                "ZGODNE" => lista.Where(t => t.Status.Contains("Zgodne")),
                _ => lista
            };
            // Problemy na górze (największa |różnica kg|), potem reszta
            var posortowane = lista.OrderByDescending(t => Math.Abs(t.RoznicaKg)).ToList();

            if (posortowane.Count == 0)
            {
                WeryfikacjaLista.Children.Add(new TextBlock { Text = "Brak pozycji w tym filtrze.", FontSize = 12, Foreground = B("#94A3B8"), Margin = new Thickness(0, 8, 0, 0) });
                return;
            }

            decimal maxKg = posortowane.Max(t => Math.Max(t.ZamowioneKg, t.ZafakturowaneKg));
            if (maxKg <= 0) maxKg = 1;

            foreach (var t in posortowane)
            {
                (string ikona, string kol) = t.Status switch
                {
                    var x when x.Contains("Zgodne") => ("✅", "#16A34A"),
                    var x when x.Contains("Ucięte") => ("✂", "#F59E0B"),
                    var x when x.Contains("Więcej") => ("➕", "#3B82F6"),
                    var x when x.Contains("Brak faktury") => ("⚠", "#DC2626"),
                    _ => ("•", "#6B7280")
                };

                var card = new Border { Background = B("#FFFFFF"), BorderBrush = B("#EEF2F7"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 8, 0, 8) };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(28) });    // ikona
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(230) });   // nazwa
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // paski
                grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(150) });   // różnica

                grid.Children.Add(WAlign(new TextBlock { Text = ikona, FontSize = 14, Foreground = B(kol), VerticalAlignment = VerticalAlignment.Center }, 0));

                var nazwaSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                nazwaSp.Children.Add(new TextBlock { Text = string.IsNullOrWhiteSpace(t.Nazwa) ? $"#{t.KodTowaru}" : t.Nazwa, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = B("#0F172A"), TextTrimming = TextTrimming.CharacterEllipsis });
                nazwaSp.Children.Add(new TextBlock { Text = t.Status, FontSize = 10, Foreground = B(kol) });
                Grid.SetColumn(nazwaSp, 1); grid.Children.Add(nazwaSp);

                // Paski zam (niebieski) + fak (zielony/czerwony)
                var bary = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 12, 0) };
                bary.Children.Add(Pasek(t.ZamowioneKg, maxKg, "#3B82F6", $"zam {t.ZamowioneKg:N0} kg"));
                bary.Children.Add(new Border { Height = 3 });
                bool nied = t.ZamowioneKg > 0 && t.ZafakturowaneKg < t.ZamowioneKg * 0.95m;
                bary.Children.Add(Pasek(t.ZafakturowaneKg, maxKg, nied ? "#DC2626" : "#16A34A", $"fak {t.ZafakturowaneKg:N0} kg"));
                Grid.SetColumn(bary, 2); grid.Children.Add(bary);

                var diffSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
                string diffKol = Math.Abs(t.RoznicaKg) < 0.5m ? "#15803D" : t.RoznicaKg < 0 ? "#B45309" : "#1E40AF";
                diffSp.Children.Add(new TextBlock { Text = $"{t.RoznicaKg:+#,##0;-#,##0;0} kg", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = B(diffKol), HorizontalAlignment = HorizontalAlignment.Right });
                diffSp.Children.Add(new TextBlock { Text = $"{t.RoznicaWartosci:+#,##0;-#,##0;0} zł", FontSize = 11, Foreground = B("#64748B"), HorizontalAlignment = HorizontalAlignment.Right });
                Grid.SetColumn(diffSp, 3); grid.Children.Add(diffSp);

                card.Child = grid;
                WeryfikacjaLista.Children.Add(card);
            }
        }

        private static UIElement WAlign(UIElement el, int col) { Grid.SetColumn(el, col); return el; }

        private Border Pasek(decimal v, decimal max, string kolor, string label)
        {
            double proc = (double)(v / max);
            var track = new Border { Height = 16, CornerRadius = new CornerRadius(4), Background = B("#EEF2F7") };
            var fg = new Grid();
            fg.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(Math.Max(0.001, proc), GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(Math.Max(0.001, 1 - proc), GridUnitType.Star) });
            var fill = new Border { CornerRadius = new CornerRadius(4), Background = B(kolor) };
            var lbl = new TextBlock { Text = label, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = B("#0F172A"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
            Grid.SetColumn(fill, 0); Grid.SetColumnSpan(lbl, 2);
            fg.Children.Add(fill); fg.Children.Add(lbl);
            track.Child = fg;
            return track;
        }

        #endregion

        #region Anulowane + wykres obrotu miesięcznego

        private void RenderAnulowaneHeader(System.Collections.Generic.List<Kalendarz1.Customer360.Models.AnulowaneZam> anul)
        {
            int liczba = anul.Count;
            decimal sumaKg = anul.Sum(a => a.SumaKg);
            decimal sumaWart = anul.Sum(a => a.Wartosc);

            if (liczba == 0)
            {
                AnulHeader.Text = "✅ Brak anulowanych zamówień w ostatnich 12 mies";
                AnulSummary.Text = "Świetna relacja z klientem — nic nie anulowano.";
            }
            else
            {
                AnulHeader.Text = $"❌ {liczba} anulowanych zamówień w ostatnich 12 mies";
                AnulSummary.Text = $"Łącznie {sumaKg:N0} kg / {sumaWart:N0} zł utraconego obrotu. Sprawdź powody.";
            }
        }

        // ── Wykres miesięczny (proste bars w canvas) ──
        private void RenderMonthlyChart(List<MonthlyStats> data)
        {
            _monthlyData = data ?? new List<MonthlyStats>();
            string Fmt(decimal v) => Customer360Format.FmtZl(v);

            // Wskaźnik kierunku trendu (zostaje — średnia pierwszej połowy vs drugiej)
            try
            {
                if (TrendKierunek != null && _monthlyData.Count >= 4)
                {
                    int polowa = _monthlyData.Count / 2;
                    decimal pierwsza = _monthlyData.Take(polowa).Average(d => d.Wartosc);
                    decimal druga = _monthlyData.Skip(_monthlyData.Count - polowa).Average(d => d.Wartosc);
                    if (pierwsza > 0)
                    {
                        decimal zmiana = (druga - pierwsza) / pierwsza * 100m;
                        string strzalka = zmiana >= 5 ? "▲ rośnie" : zmiana <= -5 ? "▼ spada" : "▬ stabilnie";
                        string kolor = zmiana >= 5 ? "#16A34A" : zmiana <= -5 ? "#DC2626" : "#64748B";
                        TrendKierunek.Text = $"{strzalka}  {(zmiana >= 0 ? "+" : "")}{zmiana:N0}%";
                        TrendKierunek.Foreground = B(kolor);
                    }
                    else TrendKierunek.Text = "";
                }
                else if (TrendKierunek != null) TrendKierunek.Text = "";
            }
            catch { }

            if (_monthlyData.Count == 0)
            {
                ChartMonthly.Series = System.Array.Empty<ISeries>();
                ChartMonthly.XAxes = new[] { new Axis() };
                ChartMonthly.YAxes = new[] { new Axis() };
                return;
            }

            var wartosci = _monthlyData.Select(d => (double)d.Wartosc).ToArray();
            var etykiety = _monthlyData.Select(d => (d.Month >= 1 && d.Month <= 12 ? MiesSkrot[d.Month - 1] : d.Month.ToString()) + $" {d.Year % 100:00}").ToArray();
            double srednia = wartosci.Length > 0 ? wartosci.Average() : 0;

            var kolumny = new ColumnSeries<double>
            {
                Values = wartosci,
                Name = "Obrót",
                Fill = new SolidColorPaint(Customer360Palette.Niebieski),
                Stroke = null,
                MaxBarWidth = 46,
                Rx = 4, Ry = 4
            };

            var liniaSr = new LineSeries<double>
            {
                Values = Enumerable.Repeat(srednia, _monthlyData.Count).ToArray(),
                Name = $"Średnia ({Fmt((decimal)srednia)})",
                Fill = null,
                GeometrySize = 0,
                Stroke = new SolidColorPaint(Customer360Palette.Zielony) { StrokeThickness = 2 }
            };

            ChartMonthly.Series = new ISeries[] { kolumny, liniaSr };
            ChartMonthly.XAxes = new[]
            {
                new Axis { Labels = etykiety, TextSize = 11, LabelsPaint = new SolidColorPaint(Customer360Palette.SzaryTekst) }
            };
            ChartMonthly.YAxes = new[]
            {
                new Axis
                {
                    MinLimit = 0,
                    Labeler = v => Fmt((decimal)v),
                    TextSize = 11,
                    LabelsPaint = new SolidColorPaint(Customer360Palette.SzaryTekst),
                    SeparatorsPaint = new SolidColorPaint(Customer360Palette.SiatkaSlaba) { StrokeThickness = 1 }
                }
            };

            // Klik słupka → szczegóły miesiąca (podpinamy raz)
            if (!_chartMonthlyWired)
            {
                ChartMonthly.DataPointerDown += (chart, points) =>
                {
                    var p = points?.FirstOrDefault();
                    if (p == null) return;
                    int idx = p.Index;
                    if (idx >= 0 && idx < _monthlyData.Count)
                    {
                        var d = _monthlyData[idx];
                        PokazSzczegolyMiesiaca(d.Year, d.Month);
                    }
                };
                _chartMonthlyWired = true;
            }
        }

        #endregion
    }
}
