using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Kalendarz1.AnalizaPrzychoduProdukcji.Models;
using Kalendarz1.AnalizaPrzychoduProdukcji.Services;
using Kalendarz1.AnalizaPrzychoduProdukcji.ViewModels;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;

namespace Kalendarz1.AnalizaPrzychoduProdukcji
{
    public partial class AnalizaPrzychoduWindow : Window
    {
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly PrzychodService _service;
        private readonly AnalizaPrzychoduViewModel _vm = new();

        private List<PrzychodRecord> _przefiltrowaneDane = new();
        private List<PrzychodRecord> _danePoprzedniegoOkresu = new();

        private readonly Dictionary<string, string> _towaryDict = new();
        private readonly Dictionary<string, string> _operatorzyDict = new();
        private readonly Dictionary<string, string> _dostawcyDict = new();
        private Dictionary<string, ArticleInfo> _articleDict = new();
        private Dictionary<string, (string CustomerID, string CustomerName)> _partiaDostawcaMap = new();

        private OperatorTypeFilter _operatorType = OperatorTypeFilter.Wszyscy;

        private bool _isWindowFullyLoaded = false;
        private bool _suppressReload = false;
        private string _wybranaPartia = null;

        private readonly DispatcherTimer _debounceTimer;
        private readonly DispatcherTimer _liveTimer;
        private readonly DispatcherTimer _liveBlinkTimer;
        private bool _liveOn = false;
        private bool _liveBlinkPhase = false;
        private CancellationTokenSource _loadCts;

        private static readonly Color AccentColor = Color.FromRgb(0x25, 0x63, 0xEB);

        public AnalizaPrzychoduWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            DataContext = _vm;

            _service = new PrzychodService(_connLibra);

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                _ = LoadDataAsync();
            };

            // LIVE: tick co 60 s odświeża dane (tylko dane, nie filtry)
            _liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _liveTimer.Tick += (s, e) => { if (_liveOn) _ = LoadDataAsync(); };

            // LIVE: pulsująca kropka co 1 s (efekt heartbeat)
            _liveBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _liveBlinkTimer.Tick += (s, e) =>
            {
                if (dotLive == null) return;
                _liveBlinkPhase = !_liveBlinkPhase;
                dotLive.Fill = new SolidColorBrush(_liveBlinkPhase
                    ? Color.FromRgb(0x10, 0xB9, 0x81)   // jasny zielony
                    : Color.FromRgb(0x05, 0x96, 0x69)); // ciemny zielony
            };

            InitializeDatesAndCombos();
            Loaded += Window_Loaded;
            KeyDown += Window_KeyDown;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _isWindowFullyLoaded = true;
            Dispatcher.BeginInvoke(new Action(async () =>
            {
                await LoadFiltersAsync();
                await LoadDataAsync();
            }), DispatcherPriority.Background);
        }

        #region Inicjalizacja

        private void InitializeDatesAndCombos()
        {
            _suppressReload = true;
            // Domyślnie 7 dni — daje kontekst, działa nawet rano w niedzielę
            dpDataDo.SelectedDate = DateTime.Today;
            dpDataOd.SelectedDate = DateTime.Today.AddDays(-6);
            _suppressReload = false;

            LoadKlasyKurczakaCombo();
            LoadGodzinyCombo();
        }

        private async Task LoadFiltersAsync()
        {
            try
            {
                var towaryTask = _service.LoadTowaryAsync(_towaryDict);
                var operatorzyTask = _service.LoadOperatorzyAsync(_operatorzyDict);
                var terminaleTask = _service.LoadTerminaleAsync();
                var partieTask = _service.LoadPartieAsync();
                var articlesTask = _service.LoadArticlesAsync();
                var dostawcyTask = _service.LoadDostawcyAsync();

                await Task.WhenAll(towaryTask, operatorzyTask, terminaleTask, partieTask, articlesTask, dostawcyTask);

                _suppressReload = true;
                cbTowar.ItemsSource = towaryTask.Result;
                cbTowar.SelectedIndex = 0;

                cbOperator.ItemsSource = operatorzyTask.Result;
                cbOperator.SelectedIndex = 0;

                cbTerminal.ItemsSource = terminaleTask.Result;
                cbTerminal.SelectedIndex = 0;

                cbPartia.ItemsSource = partieTask.Result;
                cbPartia.SelectedIndex = 0;

                if (cbDostawca != null)
                {
                    cbDostawca.ItemsSource = dostawcyTask.Result;
                    cbDostawca.SelectedIndex = 0;
                    _dostawcyDict.Clear();
                    foreach (var d in dostawcyTask.Result)
                        if (!string.IsNullOrEmpty(d.Wartosc)) _dostawcyDict[d.Wartosc] = d.Nazwa;
                }

                _articleDict = articlesTask.Result;
                _suppressReload = false;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd ładowania filtrów: {ex.Message}";
            }
        }

        private void LoadKlasyKurczakaCombo()
        {
            var klasy = new List<ComboItemString> { new() { Wartosc = "", Nazwa = "Wszystkie" } };
            for (int k = 1; k <= 12; k++)
                klasy.Add(new ComboItemString { Wartosc = k.ToString(), Nazwa = $"Klasa {k}" });

            cbKlasaKurczaka.ItemsSource = klasy;
            cbKlasaKurczaka.SelectedIndex = 0;
        }

        private void LoadGodzinyCombo()
        {
            var godzinyOd = new List<ComboItem> { new() { Id = -1, Nazwa = "Od" } };
            var godzinyDo = new List<ComboItem> { new() { Id = -1, Nazwa = "Do" } };

            for (int h = 0; h <= 23; h++)
            {
                godzinyOd.Add(new ComboItem { Id = h, Nazwa = $"{h:D2}:00" });
                godzinyDo.Add(new ComboItem { Id = h, Nazwa = $"{h:D2}:59" });
            }

            cbGodzinaOd.ItemsSource = godzinyOd;
            cbGodzinaOd.SelectedIndex = 0;
            cbGodzinaDo.ItemsSource = godzinyDo;
            cbGodzinaDo.SelectedIndex = 0;
        }

        #endregion

        #region Filter handlers

        private PrzychodFilter BuildFilter(DateTime? overrideOd = null, DateTime? overrideDo = null)
        {
            return new PrzychodFilter
            {
                DataOd = overrideOd ?? dpDataOd.SelectedDate ?? DateTime.Today,
                DataDo = overrideDo ?? dpDataDo.SelectedDate ?? DateTime.Today,
                ArticleID = cbTowar?.SelectedValue as string,
                OperatorID = cbOperator?.SelectedValue as string,
                TerminalId = cbTerminal?.SelectedValue is int t && t > 0 ? t : (int?)null,
                Partia = cbPartia?.SelectedValue as string,
                Klasa = (cbTowar?.SelectedValue as string == "40"
                         && cbKlasaKurczaka?.SelectedValue is string ks
                         && int.TryParse(ks, out int kk)) ? kk : (int?)null,
                GodzinaOd = cbGodzinaOd?.SelectedValue is int go && go >= 0 ? go : (int?)null,
                GodzinaDo = cbGodzinaDo?.SelectedValue is int gd && gd >= 0 ? gd : (int?)null,
                Dostawca = cbDostawca?.SelectedValue as string,
                TypOperatora = _operatorType
            };
        }

        private void TriggerReload()
        {
            if (!_isWindowFullyLoaded || _suppressReload) return;
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e) => TriggerReload();
        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateActiveFilterIndicator();
            TriggerReload();
        }

        private void Pill_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isWindowFullyLoaded) return;
            // Po zmianie pillsa odśwież WSZYSTKO żeby było konsystentnie
            RefreshAllViews();
        }

        private void Wzorzec_Checked(object sender, RoutedEventArgs e)
        {
            if (boxMapa == null || boxZmiany == null || boxDni == null) return;
            boxMapa.Visibility   = wzMapa.IsChecked   == true ? Visibility.Visible : Visibility.Collapsed;
            boxZmiany.Visibility = wzZmiany.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            boxDni.Visibility    = wzDni.IsChecked    == true ? Visibility.Visible : Visibility.Collapsed;

            // Po przełączeniu wzorca odśwież cały ekran (jeśli user przełącza, chce świeże dane)
            RefreshAllViews();
        }

        private void CbTowar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool kurczaki = cbTowar?.SelectedValue as string == "40";
            if (pnlKlasaKurczaka != null)
                pnlKlasaKurczaka.Visibility = kurczaki ? Visibility.Visible : Visibility.Collapsed;
            if (pnlKlasySection != null)
                pnlKlasySection.Visibility = kurczaki ? Visibility.Visible : Visibility.Collapsed;

            UpdateActiveFilterIndicator();
            TriggerReload();

            // Wymuś odświeżenie sekcji klas kurczaka (po Visibility=Visible musi się dorenderować)
            if (kurczaki && _isWindowFullyLoaded)
                Dispatcher.BeginInvoke(new Action(() => SafeUpdate("Klasy", UpdateKlasyChart)),
                    DispatcherPriority.Loaded);
        }

        // Odświeża WSZYSTKIE widoki używając już załadowanych danych (bez SQL).
        // Wywoływane po zmianie pillów, OpType, wzorca, zakładki itp.
        private void RefreshAllViews()
        {
            if (!_isWindowFullyLoaded) return;
            SafeUpdate("Statystyki", UpdateStatistics);
            SafeUpdate("Health", UpdateHealthStrip);
            SafeUpdate("Wykres", UpdatePrzychodChart);
            SafeUpdate("Operatorzy", UpdateOperatorChart);
            SafeUpdate("Partie", UpdatePartieChart);
            SafeUpdate("Towary", UpdatePrzychodyArtykuly);
            SafeUpdate("Klasy", UpdateKlasyChart);
            SafeUpdate("Tabela", UpdateDataGrids);
            SafeUpdate("Wzorce", () =>
            {
                if (wzMapa?.IsChecked == true) UpdateHeatmap();
                else if (wzZmiany?.IsChecked == true) UpdateZmianyChart();
                else if (wzDni?.IsChecked == true) UpdateDniTygodniaChart();
            });
        }

        private void ChkPorownanie_Changed(object sender, RoutedEventArgs e)
        {
            UpdateActiveFilterIndicator();
            TriggerReload();
        }

        private void BtnFiltry_Click(object sender, RoutedEventArgs e)
        {
            if (pnlFiltrySecondary == null) return;
            pnlFiltrySecondary.Visibility = pnlFiltrySecondary.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void BtnResetFiltry_Click(object sender, RoutedEventArgs e)
        {
            _suppressReload = true;
            if (cbOperator != null) cbOperator.SelectedIndex = 0;
            if (cbPartia != null) cbPartia.SelectedIndex = 0;
            if (cbTerminal != null) cbTerminal.SelectedIndex = 0;
            if (cbGodzinaOd != null) cbGodzinaOd.SelectedIndex = 0;
            if (cbGodzinaDo != null) cbGodzinaDo.SelectedIndex = 0;
            if (cbKlasaKurczaka != null) cbKlasaKurczaka.SelectedIndex = 0;
            if (cbDostawca != null) cbDostawca.SelectedIndex = 0;
            if (chkPorownanie != null) chkPorownanie.IsChecked = false;
            _suppressReload = false;
            UpdateActiveFilterIndicator();
            TriggerReload();
        }

        private void OpType_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isWindowFullyLoaded) return;
            _operatorType = opPaletuje?.IsChecked == true ? OperatorTypeFilter.TylkoPaletujacy
                          : opPorcjuje?.IsChecked == true ? OperatorTypeFilter.TylkoPorcjujacy
                          : OperatorTypeFilter.Wszyscy;
            // Odśwież wszystko – ranking + wykres + statystyki muszą być spójne
            RefreshAllViews();
        }


        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => _ = RefreshAllAsync();
        private async Task RefreshAllAsync()
        {
            await LoadFiltersAsync();
            await LoadDataAsync();
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e) => ExportToCsv();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isWindowFullyLoaded) return;
            if (e.Source != tabControl) return;

            // Po przełączeniu zakładki – odśwież wszystko, żeby świeży widok zawsze pokazywał aktualne dane
            Dispatcher.BeginInvoke(new Action(RefreshAllViews), DispatcherPriority.Loaded);
        }

        private void UpdateActiveFilterIndicator()
        {
            if (dotFiltryActive == null || icBadges == null || pnlActiveBadges == null) return;

            var badges = new List<FiltrBadge>();

            if (cbOperator?.SelectedValue is string opId && !string.IsNullOrEmpty(opId))
            {
                var nazwa = _operatorzyDict.TryGetValue(opId, out var n) ? n : opId;
                badges.Add(new FiltrBadge { Etykieta = "Operator", Wartosc = nazwa, ClearAction = () => cbOperator.SelectedIndex = 0 });
            }
            if (cbPartia?.SelectedValue is string p && !string.IsNullOrEmpty(p))
                badges.Add(new FiltrBadge { Etykieta = "Partia", Wartosc = p, ClearAction = () => cbPartia.SelectedIndex = 0 });
            if (cbTerminal?.SelectedValue is int tid && tid > 0)
                badges.Add(new FiltrBadge { Etykieta = "Terminal", Wartosc = (cbTerminal.SelectedItem as ComboItem)?.Nazwa ?? tid.ToString(), ClearAction = () => cbTerminal.SelectedIndex = 0 });
            if (cbGodzinaOd?.SelectedValue is int go && go >= 0)
                badges.Add(new FiltrBadge { Etykieta = "Od godz.", Wartosc = $"{go:D2}:00", ClearAction = () => cbGodzinaOd.SelectedIndex = 0 });
            if (cbGodzinaDo?.SelectedValue is int gd && gd >= 0)
                badges.Add(new FiltrBadge { Etykieta = "Do godz.", Wartosc = $"{gd:D2}:59", ClearAction = () => cbGodzinaDo.SelectedIndex = 0 });
            if (cbKlasaKurczaka?.SelectedValue is string kl && !string.IsNullOrEmpty(kl))
                badges.Add(new FiltrBadge { Etykieta = "Klasa", Wartosc = kl, ClearAction = () => cbKlasaKurczaka.SelectedIndex = 0 });
            if (cbDostawca?.SelectedValue is string d && !string.IsNullOrEmpty(d))
            {
                var nazwa = _dostawcyDict.TryGetValue(d, out var n) ? n : d;
                badges.Add(new FiltrBadge { Etykieta = "Dostawca", Wartosc = nazwa, ClearAction = () => cbDostawca.SelectedIndex = 0 });
            }

            dotFiltryActive.Visibility = badges.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            pnlActiveBadges.Visibility = badges.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            icBadges.Items.Clear();
            foreach (var b in badges) icBadges.Items.Add(BuildBadgeUI(b));
        }

        private Border BuildBadgeUI(FiltrBadge b)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new TextBlock
            {
                Text = $"{b.Etykieta}: ",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(AccentColor),
                VerticalAlignment = VerticalAlignment.Center
            });
            sp.Children.Add(new TextBlock
            {
                Text = b.Wartosc,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27)),
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 200,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            var btnX = new Button
            {
                Content = "×",
                Margin = new Thickness(6, 0, 0, 0),
                Padding = new Thickness(4, 0, 4, 0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Wyczyść"
            };
            btnX.Click += (_, __) => b.ClearAction?.Invoke();
            sp.Children.Add(btnX);

            return new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xBF, 0xDB, 0xFE)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 3, 6, 3),
                Margin = new Thickness(0, 0, 6, 0),
                Child = sp
            };
        }

        #endregion

        #region Presety dat

        private void ApplyPreset(DateTime od, DateTime @do)
        {
            _suppressReload = true;
            dpDataOd.SelectedDate = od;
            dpDataDo.SelectedDate = @do;
            _suppressReload = false;
            TriggerReload();
        }

        private void BtnPresetDzis_Click(object sender, RoutedEventArgs e) => ApplyPreset(DateTime.Today, DateTime.Today);
        private void BtnPresetWczoraj_Click(object sender, RoutedEventArgs e)
        {
            var d = DateTime.Today.AddDays(-1);
            ApplyPreset(d, d);
        }
        private void BtnPreset7Dni_Click(object sender, RoutedEventArgs e) => ApplyPreset(DateTime.Today.AddDays(-6), DateTime.Today);
        private void BtnPreset30Dni_Click(object sender, RoutedEventArgs e) => ApplyPreset(DateTime.Today.AddDays(-29), DateTime.Today);
        private void BtnPresetMiesiac_Click(object sender, RoutedEventArgs e)
        {
            var first = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            ApplyPreset(first, DateTime.Today);
        }

        #endregion

        #region Drill-down handlers

        private void DgRankingOperatorow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgRankingOperatorow.SelectedItem is not OperatorStats op || string.IsNullOrEmpty(op.OperatorID)) return;
            cbOperator.SelectedValue = op.OperatorID;
            EnsureFiltersExpanded();
        }

        private void DgPrzychodyArtykuly_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgPrzychodyArtykuly.SelectedItem is not ArticleStats a || string.IsNullOrEmpty(a.ArticleID)) return;
            cbTowar.SelectedValue = a.ArticleID;
        }

        private void EnsureFiltersExpanded()
        {
            if (pnlFiltrySecondary != null && pnlFiltrySecondary.Visibility != Visibility.Visible)
                pnlFiltrySecondary.Visibility = Visibility.Visible;
        }

        #endregion

        #region Partie

        private void DgPartie_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgPartie.SelectedItem is PartiaStats partia)
            {
                _wybranaPartia = partia.Partia;
                UpdatePartieDetails(partia.Partia);
            }
        }

        private void DgPartie_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgPartie.SelectedItem is not PartiaStats p) return;
            if (p.Partia == "*** RAZEM ***") return;
            cbPartia.SelectedValue = p.Partia;
            EnsureFiltersExpanded();
        }

        private void DgPartieArtykuly_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgPartieArtykuly.SelectedItem is PartiaArticleStats artykul && !string.IsNullOrEmpty(_wybranaPartia))
                ShowWazeniaWindow(_wybranaPartia, artykul.ArticleID);
        }

        private void BtnPokazWszystkieWazenia_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_wybranaPartia))
                ShowWazeniaWindow(_wybranaPartia, null);
        }

        private void UpdatePartieDetails(string partia)
        {
            if (string.IsNullOrEmpty(partia))
            {
                dgPartieArtykuly.ItemsSource = null;
                txtPartiaHeader.Text = "Szczegóły partii";
                txtPartiaInfo.Text = "Wybierz partię z listy";
                btnPokazWszystkieWazenia.Visibility = Visibility.Collapsed;
                return;
            }

            txtPartiaHeader.Text = $"Partia: {partia}";

            var artykulyPartii = _przefiltrowaneDane
                .Where(r => r.Partia == partia || (string.IsNullOrEmpty(r.Partia) && partia == "(brak partii)"))
                .GroupBy(r => r.ArticleID)
                .Select(g =>
                {
                    string articleName = g.First().NazwaTowaru;
                    if (_articleDict.TryGetValue(g.Key, out ArticleInfo artInfo) && !string.IsNullOrEmpty(artInfo.Name))
                        articleName = artInfo.Name;

                    return new PartiaArticleStats
                    {
                        ArticleID = g.Key,
                        ArticleName = articleName,
                        SumaKg = g.Sum(r => r.ActWeight),
                        LiczbaWazen = g.Count()
                    };
                })
                .OrderByDescending(a => a.SumaKg)
                .ToList();

            decimal suma = artykulyPartii.Sum(a => a.SumaKg);
            foreach (var a in artykulyPartii)
                a.Procent = suma > 0 ? (a.SumaKg / suma * 100) : 0;

            txtPartiaInfo.Text = $"{artykulyPartii.Count} artykułów · {suma:N2} kg łącznie";
            dgPartieArtykuly.ItemsSource = artykulyPartii;
            btnPokazWszystkieWazenia.Visibility = Visibility.Visible;
        }

        private void ShowWazeniaWindow(string partia, string articleId)
        {
            var wazenia = _przefiltrowaneDane
                .Where(r => (r.Partia == partia || (string.IsNullOrEmpty(r.Partia) && partia == "(brak partii)"))
                           && (string.IsNullOrEmpty(articleId) || r.ArticleID == articleId))
                .OrderByDescending(r => r.Data)
                .ThenByDescending(r => r.Godzina)
                .ToList();

            string tytul = string.IsNullOrEmpty(articleId)
                ? $"Wszystkie ważenia – Partia: {partia}"
                : $"Ważenia – Partia: {partia}, Artykuł: {articleId}";

            var window = new Window
            {
                Title = tytul,
                Width = 900,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37)),
                Padding = new Thickness(15, 10, 15, 10)
            };
            header.Child = new TextBlock
            {
                Text = $"{tytul} ({wazenia.Count} rekordów, {wazenia.Sum(w => w.ActWeight):N2} kg)",
                Foreground = Brushes.White,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var dg = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Margin = new Thickness(10),
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                RowBackground = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(0xFA, 0xFA, 0xFA))
            };
            dg.Columns.Add(new DataGridTextColumn { Header = "Data", Binding = new System.Windows.Data.Binding("Data") { StringFormat = "yyyy-MM-dd" }, Width = 90 });
            dg.Columns.Add(new DataGridTextColumn { Header = "Godzina", Binding = new System.Windows.Data.Binding("Godzina") { StringFormat = "HH:mm:ss" }, Width = 80 });
            dg.Columns.Add(new DataGridTextColumn { Header = "Towar", Binding = new System.Windows.Data.Binding("NazwaTowaru"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            dg.Columns.Add(new DataGridTextColumn { Header = "Waga kg", Binding = new System.Windows.Data.Binding("ActWeight") { StringFormat = "N2" }, Width = 90 });
            dg.Columns.Add(new DataGridTextColumn { Header = "Operator", Binding = new System.Windows.Data.Binding("Operator"), Width = 120 });
            dg.Columns.Add(new DataGridTextColumn { Header = "Terminal", Binding = new System.Windows.Data.Binding("Terminal"), Width = 80 });
            dg.Columns.Add(new DataGridTextColumn { Header = "Klasa", Binding = new System.Windows.Data.Binding("Klasa"), Width = 50 });
            dg.ItemsSource = wazenia;
            Grid.SetRow(dg, 1);
            grid.Children.Add(dg);

            var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(10) };
            var btnClose = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(20, 6, 20, 6),
                Background = new SolidColorBrush(AccentColor),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnClose.Click += (s, ev) => window.Close();
            footer.Children.Add(btnClose);
            Grid.SetRow(footer, 2);
            grid.Children.Add(footer);

            window.Content = grid;
            window.ShowDialog();
        }

        #endregion

        #region Ładowanie danych

        private async Task LoadDataAsync()
        {
            if (!dpDataOd.SelectedDate.HasValue || !dpDataDo.SelectedDate.HasValue) return;

            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var ct = _loadCts.Token;

            txtStatus.Text = "Ładowanie...";
            SetBusy(true, "Pobieram z bazy LibraNet");

            try
            {
                var filter = BuildFilter();
                var dataTask = _service.LoadDataAsync(filter);
                var partiaDostMapTask = _service.LoadPartiaDostawcaMapAsync(
                    filter.DataOd.ToString("yyyy-MM-dd"),
                    filter.DataDo.ToString("yyyy-MM-dd"));

                Task<List<PrzychodRecord>> prevTask = null;
                if (chkPorownanie?.IsChecked == true)
                {
                    int dni = Math.Max(1, (filter.DataDo.Date - filter.DataOd.Date).Days + 1);
                    var prevFilter = BuildFilter(filter.DataOd.AddDays(-dni), filter.DataOd.AddDays(-1));
                    prevTask = _service.LoadDataAsync(prevFilter);
                }

                if (prevTask != null) await Task.WhenAll(dataTask, partiaDostMapTask, prevTask);
                else await Task.WhenAll(dataTask, partiaDostMapTask);

                if (ct.IsCancellationRequested) return;

                _przefiltrowaneDane = dataTask.Result;
                _partiaDostawcaMap = partiaDostMapTask.Result;
                _danePoprzedniegoOkresu = prevTask?.Result ?? new List<PrzychodRecord>();

                SetBusy(true, "Aktualizuję widoki…");

                SafeUpdate("Statystyki", UpdateStatistics);
                SafeUpdate("Health", UpdateHealthStrip);
                SafeUpdate("Wykres", UpdatePrzychodChart);
                SafeUpdate("Operatorzy", UpdateOperatorChart);
                SafeUpdate("Partie", UpdatePartieChart);
                SafeUpdate("Towary", UpdatePrzychodyArtykuly);
                SafeUpdate("Klasy", UpdateKlasyChart);
                SafeUpdate("Tabela", UpdateDataGrids);
                SafeUpdate("Wzorce", () =>
                {
                    if (wzMapa?.IsChecked == true) UpdateHeatmap();
                    else if (wzZmiany?.IsChecked == true) UpdateZmianyChart();
                    else if (wzDni?.IsChecked == true) UpdateDniTygodniaChart();
                });

                txtLastRefresh.Text = $"Aktualizacja: {DateTime.Now:HH:mm:ss}" + (_liveOn ? " 🟢" : "");
                txtStatus.Text = $"{_przefiltrowaneDane.Count} rekordów"
                    + (chkPorownanie?.IsChecked == true ? $" · poprzedni okres: {_danePoprzedniegoOkresu.Count}" : "");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd:\n{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}",
                    "Błąd ładowania danych", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Błąd ładowania danych";
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy, string details = null)
        {
            if (loadingOverlay == null) return;
            loadingOverlay.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
            if (busy && details != null && txtLoadingDetails != null)
                txtLoadingDetails.Text = details;
        }

        private void SafeUpdate(string name, Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd ({name}): {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[AnalizaPrzychodu] {name}: {ex}");
            }
        }

        #endregion

        #region Statystyki

        private void UpdateStatistics()
        {
            if (txtSumaKg == null) return;

            // Suma uwzględnia storno (ujemne = anulacje, kompensują się z dodatnimi)
            decimal sumaKg = _przefiltrowaneDane.Sum(r => r.ActWeight);
            // Rzeczywiste ważenia = tylko dodatnie. Anulacje liczymy osobno.
            int liczbaWazenRealnych = _przefiltrowaneDane.Count(r => r.ActWeight > 0);
            int liczbaAnulacji = _przefiltrowaneDane.Count(r => r.ActWeight < 0);
            int liczbaDni = _przefiltrowaneDane.Select(r => r.Data.Date).Distinct().Count();
            decimal sredniaDzien = liczbaDni > 0 ? sumaKg / liczbaDni : 0;

            txtSumaKg.Text = $"{sumaKg:N0} kg";
            txtSumaKgInfo.Text = liczbaDni > 0 ? $"w {liczbaDni} dni" : "";

            txtLiczbaWazen.Text = $"{liczbaWazenRealnych:N0}";
            txtLiczbaWazenInfo.Text = liczbaAnulacji > 0
                ? $"⚠ {liczbaAnulacji} anulacji"
                : (liczbaDni > 0 ? $"~{liczbaWazenRealnych / liczbaDni} dziennie" : "");

            txtSredniaDzien.Text = $"{sredniaDzien:N0} kg";
            txtSredniaDzienInfo.Text = "dzienna produkcja";

            UpdateOdchylenieCard();
            UpdateTrendCard(sumaKg);
        }

        private void UpdateOdchylenieCard()
        {
            if (txtOdchylenie == null) return;

            // Suma różnic ActWeight - Weight (tylko dodatnie ważenia, ignorujemy storno)
            // Weight musi być > 0 żeby było sensowne porównanie
            var rekordy = _przefiltrowaneDane.Where(r => r.ActWeight > 0 && r.Weight > 0).ToList();
            if (rekordy.Count == 0)
            {
                txtOdchylenie.Text = "—";
                txtOdchylenie.Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
                txtOdchylenieInfo.Text = "brak danych";
                return;
            }

            decimal sumaOdchylenia = rekordy.Sum(r => r.ActWeight - r.Weight);
            decimal sumaStandard = rekordy.Sum(r => r.Weight);
            decimal procent = sumaStandard != 0 ? sumaOdchylenia / sumaStandard * 100m : 0;
            int dokladamyCnt = rekordy.Count(r => r.Dokladamy);
            int niedowagaCnt = rekordy.Count(r => r.Niedowaga);

            string znak = sumaOdchylenia >= 0 ? "+" : "";
            txtOdchylenie.Text = $"{znak}{sumaOdchylenia:N1} kg";
            // Plus (dokładamy) = czerwono (strata firmy), minus (niedowaga) = zielono (zysk firmy ale risk dla klienta)
            txtOdchylenie.Foreground = sumaOdchylenia > 0
                ? new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26))
                : sumaOdchylenia < 0
                    ? new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69))
                    : new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
            txtOdchylenieInfo.Text = $"{procent:+0.00;-0.00;0.00}% · ↑{dokladamyCnt} ↓{niedowagaCnt}";
        }

        private void UpdateTrendCard(decimal sumaKg)
        {
            if (txtTrend == null) return;

            if (chkPorownanie?.IsChecked != true)
            {
                txtTrend.Text = "—";
                txtTrend.Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
                txtTrendInfo.Text = "włącz „Porównaj okres\"";
                return;
            }

            decimal prev = _danePoprzedniegoOkresu.Sum(r => r.ActWeight);
            if (prev == 0)
            {
                txtTrend.Text = sumaKg > 0 ? "▲ nowy" : "—";
                txtTrend.Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));
                txtTrendInfo.Text = "brak danych w poprz. okresie";
                return;
            }

            decimal pct = (sumaKg - prev) / prev * 100m;
            string arrow = pct >= 0 ? "▲" : "▼";
            txtTrend.Text = $"{arrow} {pct:+0.0;-0.0;0.0}%";
            txtTrend.Foreground = pct >= 0
                ? new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69))
                : new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            txtTrendInfo.Text = $"poprz.: {prev:N0} kg";
        }

        #endregion

        #region Wykres główny

        private string GetCurrentGrouping()
        {
            if (pillOperator?.IsChecked == true) return "Operator";
            if (pillTowar?.IsChecked == true) return "Towar";
            if (pillPartia?.IsChecked == true) return "Partia";
            if (pillTerminal?.IsChecked == true) return "Terminal";
            if (pillDzien?.IsChecked == true) return "Dzien";
            return "Godzina";
        }

        private void UpdatePrzychodChart()
        {
            if (chartPrzychod == null) return;

            try { chartPrzychod.Series.Clear(); } catch { }

            bool hasData = _przefiltrowaneDane?.Any() == true;
            if (emptyWykres != null)
                emptyWykres.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;

            if (!hasData)
            {
                _vm.PrzychodLabels = new List<string>();
                _vm.PrzychodValues = new ChartValues<double>();
                return;
            }

            var grupowanie = GetCurrentGrouping();
            IEnumerable<KeyValuePair<string, decimal>> grupy;

            switch (grupowanie)
            {
                case "Operator":
                    grupy = _przefiltrowaneDane
                        .GroupBy(r => string.IsNullOrEmpty(r.Operator) ? "(brak)" : r.Operator)
                        .Select(g => new KeyValuePair<string, decimal>(g.Key, g.Sum(r => r.ActWeight)))
                        .OrderByDescending(g => g.Value);
                    break;
                case "Towar":
                    grupy = _przefiltrowaneDane
                        .GroupBy(r => r.NazwaTowaru ?? "")
                        .Select(g => new KeyValuePair<string, decimal>(
                            g.Key.Length > 22 ? g.Key.Substring(0, 22) + "…" : g.Key,
                            g.Sum(r => r.ActWeight)))
                        .OrderByDescending(g => g.Value);
                    break;
                case "Partia":
                    grupy = _przefiltrowaneDane
                        .GroupBy(r => string.IsNullOrEmpty(r.Partia) ? "(brak)" : r.Partia)
                        .Select(g => new KeyValuePair<string, decimal>(g.Key, g.Sum(r => r.ActWeight)))
                        .OrderByDescending(g => g.Value);
                    break;
                case "Terminal":
                    grupy = _przefiltrowaneDane
                        .GroupBy(r => string.IsNullOrEmpty(r.Terminal) ? $"T{r.TermID}" : r.Terminal)
                        .Select(g => new KeyValuePair<string, decimal>(g.Key, g.Sum(r => r.ActWeight)))
                        .OrderByDescending(g => g.Value);
                    break;
                case "Dzien":
                    grupy = _przefiltrowaneDane
                        .GroupBy(r => r.Data.Date)
                        .Select(g => new KeyValuePair<string, decimal>(
                            g.Key.ToString("MM-dd ddd", new CultureInfo("pl-PL")),
                            g.Sum(r => r.ActWeight)))
                        .OrderBy(g => g.Key);
                    break;
                default:
                    grupy = _przefiltrowaneDane
                        .GroupBy(r => r.Godzina.Hour)
                        .Select(g => new KeyValuePair<string, decimal>($"{g.Key:D2}:00", g.Sum(r => r.ActWeight)))
                        .OrderBy(g => g.Key);
                    break;
            }

            var listaGrup = grupy.ToList();
            _vm.PrzychodLabels = listaGrup.Select(g => g.Key).ToList();
            _vm.PrzychodValues = new ChartValues<double>(listaGrup.Select(g => (double)g.Value));

            try
            {
                chartPrzychod.Series.Add(new ColumnSeries
                {
                    Title = "Przychód (kg)",
                    Values = _vm.PrzychodValues,
                    Fill = new SolidColorBrush(AccentColor),
                    MaxColumnWidth = 50,
                    DataLabels = listaGrup.Count <= 24,
                    LabelPoint = point => $"{point.Y:N0}"
                });
            }
            catch (NullReferenceException)
            {
                Dispatcher.BeginInvoke(new Action(UpdatePrzychodChart), DispatcherPriority.Background);
            }
        }

        #endregion

        #region Operatorzy

        private void UpdateOperatorChart()
        {
            if (dgRankingOperatorow == null) return;

            try { chartOperatorzy?.Series?.Clear(); } catch { }

            bool hasData = _przefiltrowaneDane?.Any() == true;
            if (emptyOperatorzy != null)
                emptyOperatorzy.Visibility = hasData ? Visibility.Collapsed : Visibility.Visible;

            if (!hasData)
            {
                _vm.OperatorLabels = new List<string>();
                _vm.OperatorSumaValues = new ChartValues<double>();
                dgRankingOperatorow.ItemsSource = null;
                return;
            }

            // Filtr typu operatora: Paletujący (głównie ArticleID=40) vs Porcjujący (reszta)
            var dane = _przefiltrowaneDane.AsEnumerable();
            if (_operatorType == OperatorTypeFilter.TylkoPaletujacy)
                dane = dane.Where(r => r.ArticleID == "40");
            else if (_operatorType == OperatorTypeFilter.TylkoPorcjujacy)
                dane = dane.Where(r => r.ArticleID != "40");

            var grupyOperatorow = dane
                .GroupBy(r => r.Operator ?? "")
                .Select(g => new OperatorStats
                {
                    Nazwa = string.IsNullOrEmpty(g.Key) ? "(brak)" : g.Key,
                    OperatorID = g.First().OperatorID,
                    SumaKg = g.Sum(r => r.ActWeight),
                    LiczbaWazen = g.Count(r => r.ActWeight > 0),
                    LiczbaAnulacji = g.Count(r => r.ActWeight < 0),
                    SredniaKg = g.Where(r => r.ActWeight > 0).DefaultIfEmpty().Average(r => r?.ActWeight ?? 0),
                    Paletuje = g.Count(r => r.ArticleID == "40") > g.Count() / 2
                })
                .OrderByDescending(g => g.SumaKg)
                .ToList();

            for (int i = 0; i < grupyOperatorow.Count; i++)
                grupyOperatorow[i].Pozycja = i + 1;

            // Sparkline: udział vs. lider (0..100)
            decimal maxKg = grupyOperatorow.Count > 0 ? grupyOperatorow.Max(g => g.SumaKg) : 0;
            foreach (var op in grupyOperatorow)
                op.PctMax = maxKg > 0 ? (double)(op.SumaKg / maxKg * 100m) : 0;

            _vm.OperatorLabels = grupyOperatorow.Select(g => g.Nazwa).ToList();
            _vm.OperatorSumaValues = new ChartValues<double>(grupyOperatorow.Select(g => (double)g.SumaKg));

            try
            {
                chartOperatorzy.Series.Add(new ColumnSeries
                {
                    Title = "Suma kg",
                    Values = _vm.OperatorSumaValues,
                    Fill = new SolidColorBrush(AccentColor),
                    MaxColumnWidth = 60,
                    DataLabels = grupyOperatorow.Count <= 12,
                    LabelPoint = point => $"{point.Y:N0}"
                });
            }
            catch (NullReferenceException)
            {
                Dispatcher.BeginInvoke(new Action(UpdateOperatorChart), DispatcherPriority.Background);
            }

            dgRankingOperatorow.ItemsSource = grupyOperatorow;
        }

        #endregion

        #region Partie

        private void UpdatePartieChart()
        {
            if (!_isWindowFullyLoaded || dgPartie == null) return;

            if (!_przefiltrowaneDane.Any())
            {
                dgPartie.ItemsSource = null;
                return;
            }

            var grupyPartii = _przefiltrowaneDane
                .GroupBy(r => string.IsNullOrEmpty(r.Partia) ? "(brak partii)" : r.Partia)
                .Select(g => new PartiaStats
                {
                    Partia = g.Key,
                    SumaKg = g.Sum(r => r.ActWeight),
                    Liczba = g.Count(r => r.ActWeight > 0),
                    LiczbaAnulacji = g.Count(r => r.ActWeight < 0),
                    Dostawca = _partiaDostawcaMap.TryGetValue(g.Key, out var d) ? d.CustomerName : "",
                    CustomerID = _partiaDostawcaMap.TryGetValue(g.Key, out var d2) ? d2.CustomerID : ""
                })
                .OrderByDescending(g => g.Partia)
                .ToList();

            // Procent liczony od sumy przefiltrowanych partii – pokazuje udział partii
            // w obrębie aktualnego filtra (np. ile Fileta z każdej partii)
            decimal suma = grupyPartii.Sum(p => p.SumaKg);
            foreach (var p in grupyPartii)
                p.Procent = suma > 0 ? (p.SumaKg / suma * 100) : 0;

            dgPartie.ItemsSource = grupyPartii;
        }

        #endregion

        #region Towary

        private void UpdatePrzychodyArtykuly()
        {
            if (!_isWindowFullyLoaded || dgPrzychodyArtykuly == null) return;

            if (!_przefiltrowaneDane.Any())
            {
                dgPrzychodyArtykuly.ItemsSource = null;
                return;
            }

            var grupyArtykulow = _przefiltrowaneDane
                .GroupBy(r => r.ArticleID)
                .Select(g =>
                {
                    string shortName = "";
                    string articleName = g.First().NazwaTowaru;
                    if (_articleDict.TryGetValue(g.Key, out ArticleInfo artInfo))
                    {
                        shortName = artInfo.ShortName;
                        if (!string.IsNullOrEmpty(artInfo.Name)) articleName = artInfo.Name;
                    }
                    return new ArticleStats
                    {
                        ArticleID = g.Key,
                        ShortName = shortName,
                        ArticleName = articleName,
                        SumaKg = g.Sum(r => r.ActWeight),
                        LiczbaWazen = g.Count(),
                        SredniaKg = g.Average(r => r.ActWeight)
                    };
                })
                .OrderByDescending(g => g.SumaKg)
                .ToList();

            // Procent liczony od sumy przefiltrowanych artykułów
            decimal suma = grupyArtykulow.Sum(a => a.SumaKg);
            decimal kumul = 0;
            foreach (var a in grupyArtykulow)
            {
                a.Procent = suma > 0 ? (a.SumaKg / suma * 100) : 0;
                kumul += a.Procent;
                a.KumulacyjnyPct = kumul;
                a.TopPareto = kumul <= 80m;  // Pareto: top towary do 80% wolumenu
            }

            dgPrzychodyArtykuly.ItemsSource = grupyArtykulow;
        }

        private void UpdateKlasyChart()
        {
            if (!_isWindowFullyLoaded || chartKlasy == null || dgKlasy == null) return;

            // Klasy widoczne tylko gdy ArticleID=40 (kurczak) — z aktywnymi filtrami
            var daneKurczakow = _przefiltrowaneDane.Where(r => r.ArticleID == "40").ToList();
            if (!daneKurczakow.Any())
            {
                dgKlasy.ItemsSource = null;
                try { chartKlasy.Series.Clear(); } catch { }
                return;
            }

            var grupyKlas = daneKurczakow
                .GroupBy(r => r.Klasa)
                .Select(g => new KlasaStats
                {
                    Klasa = g.Key,
                    SumaKg = g.Sum(r => r.ActWeight),
                    Liczba = g.Count(),
                    SredniaKg = g.Average(r => r.ActWeight)
                })
                .OrderBy(g => g.Klasa)
                .ToList();

            decimal suma = grupyKlas.Sum(k => k.SumaKg);
            foreach (var k in grupyKlas)
                k.Procent = suma > 0 ? (k.SumaKg / suma * 100) : 0;

            try
            {
                chartKlasy.Series.Clear();
                var paleta = new[] {
                    Color.FromRgb(0x25, 0x63, 0xEB), Color.FromRgb(0x05, 0x96, 0x69),
                    Color.FromRgb(0xDC, 0x26, 0x26), Color.FromRgb(0xD9, 0x77, 0x06),
                    Color.FromRgb(0x9D, 0x17, 0x4D), Color.FromRgb(0x05, 0x88, 0x87),
                    Color.FromRgb(0x65, 0x4D, 0xD0), Color.FromRgb(0x37, 0x41, 0x51),
                    Color.FromRgb(0x6B, 0x72, 0x80), Color.FromRgb(0x9C, 0xA3, 0xAF),
                    Color.FromRgb(0xD1, 0xD5, 0xDB), Color.FromRgb(0x11, 0x18, 0x27)
                };

                foreach (var klasa in grupyKlas)
                {
                    int kolorIndex = (klasa.Klasa - 1) % paleta.Length;
                    chartKlasy.Series.Add(new PieSeries
                    {
                        Title = $"Klasa {klasa.Klasa}",
                        Values = new ChartValues<double> { (double)klasa.SumaKg },
                        Fill = new SolidColorBrush(paleta[kolorIndex]),
                        DataLabels = true,
                        LabelPoint = point => $"Kl.{klasa.Klasa}: {klasa.Procent:N1}%"
                    });
                }
            }
            catch (NullReferenceException)
            {
                Dispatcher.BeginInvoke(new Action(UpdateKlasyChart), DispatcherPriority.Background);
                return;
            }

            dgKlasy.ItemsSource = grupyKlas;
        }

        #endregion

        #region Wzorce: Zmiany / Dni tygodnia / Heatmap

        // Granice zmian: dzienna 5–21, nocna 21–5
        private const int DAY_SHIFT_START = 5;
        private const int NIGHT_SHIFT_START = 21;

        private static bool IsDayShift(int hour) => hour >= DAY_SHIFT_START && hour < NIGHT_SHIFT_START;
        private static bool IsNightShift(int hour) => hour >= NIGHT_SHIFT_START || hour < DAY_SHIFT_START;

        private void UpdateZmianyChart()
        {
            if (!_isWindowFullyLoaded || chartZmiany == null) return;

            decimal sumaDzienna = _przefiltrowaneDane.Where(r => IsDayShift(r.Godzina.Hour)).Sum(r => r.ActWeight);
            decimal sumaNocna   = _przefiltrowaneDane.Where(r => IsNightShift(r.Godzina.Hour)).Sum(r => r.ActWeight);
            int liczbaDzienna = _przefiltrowaneDane.Count(r => IsDayShift(r.Godzina.Hour) && r.ActWeight > 0);
            int liczbaNocna   = _przefiltrowaneDane.Count(r => IsNightShift(r.Godzina.Hour) && r.ActWeight > 0);

            if (txtZmianaDzienna != null)
            {
                txtZmianaDzienna.Text = $"{sumaDzienna:N0} kg";
                txtZmianaDziennaSzt.Text = $"{liczbaDzienna} ważeń";
            }
            if (txtZmianaNocna != null)
            {
                txtZmianaNocna.Text = $"{sumaNocna:N0} kg";
                txtZmianaNocnaSzt.Text = $"{liczbaNocna} ważeń";
            }
            if (txtNajlepszaZmiana != null)
            {
                if (sumaDzienna == 0 && sumaNocna == 0)
                    txtNajlepszaZmiana.Text = "–";
                else
                {
                    var max = new[] { ("Dzienna", sumaDzienna), ("Nocna", sumaNocna) }
                        .OrderByDescending(x => x.Item2).First();
                    txtNajlepszaZmiana.Text = $"{max.Item1} ({max.Item2:N0} kg)";
                }
            }

            try
            {
                chartZmiany.Series.Clear();
                chartZmiany.Series.Add(new ColumnSeries
                {
                    Title = "Suma kg",
                    Values = new ChartValues<double> { (double)sumaDzienna, (double)sumaNocna },
                    Fill = new SolidColorBrush(AccentColor),
                    MaxColumnWidth = 80,
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:N0}"
                });
            }
            catch (NullReferenceException)
            {
                Dispatcher.BeginInvoke(new Action(UpdateZmianyChart), DispatcherPriority.Background);
            }
        }

        private void UpdateDniTygodniaChart()
        {
            if (!_isWindowFullyLoaded || chartDniTygodnia == null || dgDniTygodnia == null) return;

            try { chartDniTygodnia.Series.Clear(); } catch { }

            if (!_przefiltrowaneDane.Any())
            {
                dgDniTygodnia.ItemsSource = null;
                return;
            }

            var nazwyDni = new[] { "Poniedziałek", "Wtorek", "Środa", "Czwartek", "Piątek", "Sobota", "Niedziela" };

            var grupyDni = _przefiltrowaneDane
                .GroupBy(r => (int)r.Data.DayOfWeek)
                .Select(g => new DzienTygodniaStats
                {
                    DzienNumer = g.Key == 0 ? 7 : g.Key,
                    DzienTygodnia = nazwyDni[g.Key == 0 ? 6 : g.Key - 1],
                    SumaKg = g.Sum(r => r.ActWeight),
                    LiczbaDni = g.Select(r => r.Data.Date).Distinct().Count(),
                    SredniaKg = g.Sum(r => r.ActWeight) / Math.Max(1, g.Select(r => r.Data.Date).Distinct().Count())
                })
                .OrderBy(g => g.DzienNumer)
                .ToList();

            var wartosci = new double[7];
            foreach (var dzien in grupyDni)
            {
                int index = dzien.DzienNumer - 1;
                if (index >= 0 && index < 7) wartosci[index] = (double)dzien.SredniaKg;
            }

            try
            {
                chartDniTygodnia.Series.Add(new ColumnSeries
                {
                    Title = "Średnia kg/dzień",
                    Values = new ChartValues<double>(wartosci),
                    Fill = new SolidColorBrush(AccentColor),
                    MaxColumnWidth = 60
                });
            }
            catch (NullReferenceException)
            {
                Dispatcher.BeginInvoke(new Action(UpdateDniTygodniaChart), DispatcherPriority.Background);
                return;
            }

            dgDniTygodnia.ItemsSource = grupyDni;
        }

        private void UpdateHeatmap()
        {
            if (!_isWindowFullyLoaded || icHeatmap == null) return;

            try
            {
                icHeatmap.Items.Clear();

                var dniUnique = _przefiltrowaneDane?
                    .Select(r => r.Data.Date)
                    .Where(d => d != DateTime.MinValue)
                    .Distinct()
                    .OrderByDescending(d => d)
                    .Take(14)         // max 14 dni
                    .OrderBy(d => d)
                    .ToList() ?? new List<DateTime>();

                if (dniUnique.Count == 0)
                {
                    icHeatmap.Items.Add(new TextBlock
                    {
                        Text = "📭 Brak danych do wyświetlenia mapy cieplnej",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
                        Margin = new Thickness(20),
                        HorizontalAlignment = HorizontalAlignment.Center
                    });
                    return;
                }

                int godzinaOd = 3, godzinaDo = 23;

                var daneHeatmap = new Dictionary<(DateTime, int), decimal>();
                for (int h = godzinaOd; h <= godzinaDo; h++)
                {
                    foreach (var dzien in dniUnique)
                    {
                        daneHeatmap[(dzien, h)] = _przefiltrowaneDane
                            .Where(r => r.Data.Date == dzien && r.Godzina.Hour == h)
                            .Sum(r => r.ActWeight);
                    }
                }

                var statystykiGodzin = new Dictionary<int, (decimal min, decimal max)>();
                for (int h = godzinaOd; h <= godzinaDo; h++)
                {
                    var wartosci = dniUnique.Select(d => daneHeatmap[(d, h)]).Where(v => v > 0).ToList();
                    statystykiGodzin[h] = wartosci.Any() ? (wartosci.Min(), wartosci.Max()) : (0, 0);
                }

                // Header z godzinami
                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                headerPanel.Children.Add(MakeCell("Data", 80, 28, Color.FromRgb(0xF3, 0xF4, 0xF6),
                    new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)), FontWeights.SemiBold, 11));

                for (int h = godzinaOd; h <= godzinaDo; h++)
                    headerPanel.Children.Add(MakeCell($"{h}:00", 46, 28, Color.FromRgb(0xF3, 0xF4, 0xF6),
                        new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)), FontWeights.SemiBold, 10));
                icHeatmap.Items.Add(headerPanel);

                // Wiersze
                foreach (var dzien in dniUnique)
                {
                    var rowPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    rowPanel.Children.Add(MakeCell(
                        dzien.ToString("MM-dd ddd", new CultureInfo("pl-PL")),
                        80, 26, Color.FromRgb(0xFA, 0xFA, 0xFA),
                        new SolidColorBrush(Color.FromRgb(0x11, 0x18, 0x27)), FontWeights.SemiBold, 10));

                    for (int h = godzinaOd; h <= godzinaDo; h++)
                    {
                        var wartosc = daneHeatmap[(dzien, h)];
                        var (min, max) = statystykiGodzin[h];
                        Color kolorTla = HeatmapColor(wartosc, min, max);
                        Brush textColor = wartosc == 0
                            ? (Brush)new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0))
                            : Brushes.Black;

                        rowPanel.Children.Add(MakeCell(
                            wartosc > 0 ? $"{wartosc:N0}" : "–",
                            46, 26, kolorTla, textColor,
                            wartosc > 0 ? FontWeights.SemiBold : FontWeights.Normal, 9));
                    }
                    icHeatmap.Items.Add(rowPanel);
                }
            }
            catch (Exception ex)
            {
                icHeatmap.Items.Add(new TextBlock
                {
                    Text = $"Błąd mapy cieplnej: {ex.Message}",
                    FontSize = 12,
                    Foreground = Brushes.Red,
                    Margin = new Thickness(20)
                });
            }
        }

        // Gradient kolorów: czerwony (0%) → żółty (50%) → zielony (100%)
        private static Color HeatmapColor(decimal wartosc, decimal min, decimal max)
        {
            if (wartosc == 0) return Color.FromRgb(0xF9, 0xFA, 0xFB);
            if (max == min) return Color.FromRgb(0xFD, 0xE6, 0x8A); // żółty
            double normalized = (double)(wartosc - min) / (double)(max - min);
            normalized = Math.Max(0, Math.Min(1, normalized));
            // 0..120 stopni hue
            double hue = 120.0 * normalized;
            return HslToRgb(hue, 0.55, 0.65);
        }

        private static Color HslToRgb(double h, double s, double l)
        {
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double hh = h / 60.0;
            double x = c * (1 - Math.Abs(hh % 2 - 1));
            double r = 0, g = 0, b = 0;
            if (hh >= 0 && hh < 1) { r = c; g = x; }
            else if (hh < 2) { r = x; g = c; }
            else if (hh < 3) { g = c; b = x; }
            else if (hh < 4) { g = x; b = c; }
            else if (hh < 5) { r = x; b = c; }
            else { r = c; b = x; }
            double m = l - c / 2;
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        private static Border MakeCell(string text, double width, double height, Color bg, Brush fg, FontWeight weight, double fontSize)
        {
            return new Border
            {
                Width = width,
                Height = height,
                Background = new SolidColorBrush(bg),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0xE7, 0xEB)),
                BorderThickness = new Thickness(1, 0, 0, 1),
                Child = new TextBlock
                {
                    Text = text,
                    Foreground = fg,
                    FontWeight = weight,
                    FontSize = fontSize,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
        }

        #endregion

        #region Tabela

        private void UpdateDataGrids()
        {
            if (dgSzczegoly == null) return;

            dgSzczegoly.ItemsSource = _przefiltrowaneDane
                .OrderByDescending(r => r.Data)
                .ThenByDescending(r => r.Godzina)
                .ToList();

            if (txtLiczbaWierszy != null)
                txtLiczbaWierszy.Text = $"{_przefiltrowaneDane.Count} rekordów";
        }

        #endregion

        #region LIVE mode (auto-refresh)

        private void BtnLive_Click(object sender, RoutedEventArgs e) => ToggleLive();

        private void ToggleLive()
        {
            _liveOn = !_liveOn;
            if (_liveOn)
            {
                btnLive.Style = (Style)FindResource("BtnLiveOn");
                txtLiveLabel.Text = "LIVE • 60 s";
                _liveTimer.Start();
                _liveBlinkTimer.Start();
                _ = LoadDataAsync();
            }
            else
            {
                btnLive.Style = (Style)FindResource("BtnLiveOff");
                txtLiveLabel.Text = "LIVE";
                _liveTimer.Stop();
                _liveBlinkTimer.Stop();
                if (dotLive != null)
                    dotLive.Fill = new SolidColorBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
            }
        }

        #endregion

        #region Skróty klawiszowe

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // F5 = odśwież
            if (e.Key == System.Windows.Input.Key.F5)
            {
                _ = RefreshAllAsync();
                e.Handled = true;
                return;
            }

            bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            if (ctrl)
            {
                switch (e.Key)
                {
                    case System.Windows.Input.Key.E: ExportToCsv(); e.Handled = true; return;
                    case System.Windows.Input.Key.L: ToggleLive(); e.Handled = true; return;
                    case System.Windows.Input.Key.D1: tabControl.SelectedIndex = 0; e.Handled = true; return;
                    case System.Windows.Input.Key.D2: tabControl.SelectedIndex = 1; e.Handled = true; return;
                    case System.Windows.Input.Key.D3: tabControl.SelectedIndex = 2; e.Handled = true; return;
                    case System.Windows.Input.Key.D4: tabControl.SelectedIndex = 3; e.Handled = true; return;
                    case System.Windows.Input.Key.D5: tabControl.SelectedIndex = 4; e.Handled = true; return;
                    case System.Windows.Input.Key.D6: tabControl.SelectedIndex = 5; e.Handled = true; return;
                }
            }

            // Esc = wyczyść filtry (jeżeli aktywne)
            if (e.Key == System.Windows.Input.Key.Escape)
            {
                if (dotFiltryActive?.Visibility == Visibility.Visible)
                {
                    BtnResetFiltry_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                }
            }
        }

        #endregion

        #region Drill-down z kart KPI

        private void StatCardSuma_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            tabControl.SelectedIndex = 0; // Wykres
        }

        private void StatCardLiczba_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            tabControl.SelectedIndex = 5; // Tabela
        }

        private void StatCardSrednia_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            tabControl.SelectedIndex = 0; // Wykres
            if (pillDzien != null) pillDzien.IsChecked = true;
        }

        private void StatCardOdchylenie_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            tabControl.SelectedIndex = 5; // Tabela
            // Posortuj po Roznica (kol. 5) malejąco — pokazuje największe dokładania na górze
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (dgSzczegoly?.Columns?.Count > 5)
                {
                    var col = dgSzczegoly.Columns[5];
                    col.SortDirection = System.ComponentModel.ListSortDirection.Descending;
                    var view = System.Windows.Data.CollectionViewSource.GetDefaultView(dgSzczegoly.ItemsSource);
                    if (view != null)
                    {
                        view.SortDescriptions.Clear();
                        view.SortDescriptions.Add(new System.ComponentModel.SortDescription("Roznica",
                            System.ComponentModel.ListSortDirection.Descending));
                    }
                }
            }), DispatcherPriority.Loaded);
        }

        private void StatCardTrend_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            EnsureFiltersExpanded();
            if (chkPorownanie != null && chkPorownanie.IsChecked != true)
                chkPorownanie.IsChecked = true;
            tabControl.SelectedIndex = 0;
            if (pillDzien != null) pillDzien.IsChecked = true;
        }

        #endregion

        #region Health Strip

        private void UpdateHealthStrip()
        {
            if (pnlHealthStrip == null || icHealthItems == null) return;

            if (!_przefiltrowaneDane.Any())
            {
                pnlHealthStrip.Visibility = Visibility.Collapsed;
                return;
            }

            var items = new List<(string icon, string text, string color)>();

            // 1) Anulacje
            int anul = _przefiltrowaneDane.Count(r => r.ActWeight < 0);
            if (anul > 0)
                items.Add(("⚠", $"{anul} anulacji w okresie", "warn"));

            // 2) Odchylenie
            var rekordyZWaga = _przefiltrowaneDane.Where(r => r.ActWeight > 0 && r.Weight > 0).ToList();
            if (rekordyZWaga.Count > 0)
            {
                decimal sumaOdch = rekordyZWaga.Sum(r => r.ActWeight - r.Weight);
                decimal sumaStd = rekordyZWaga.Sum(r => r.Weight);
                decimal pct = sumaStd != 0 ? sumaOdch / sumaStd * 100m : 0;
                if (Math.Abs(pct) >= 1m)
                {
                    string znak = pct > 0 ? "↑" : "↓";
                    string opis = pct > 0 ? "dokładamy" : "niedoważamy";
                    items.Add(("⚖", $"{znak} {Math.Abs(pct):N2}% {opis}", Math.Abs(pct) >= 2m ? "alert" : "warn"));
                }
            }

            // 3) Top operator wykonuje >50% wolumenu (potencjalna luka kadrowa)
            var topOp = _przefiltrowaneDane
                .Where(r => r.ActWeight > 0 && !string.IsNullOrEmpty(r.Operator))
                .GroupBy(r => r.Operator)
                .Select(g => new { Op = g.Key, Sum = g.Sum(r => r.ActWeight) })
                .OrderByDescending(g => g.Sum).FirstOrDefault();
            decimal sumaCalk = _przefiltrowaneDane.Where(r => r.ActWeight > 0).Sum(r => r.ActWeight);
            if (topOp != null && sumaCalk > 0)
            {
                decimal udzial = topOp.Sum / sumaCalk * 100m;
                if (udzial >= 50m)
                    items.Add(("👤", $"{topOp.Op} = {udzial:N0}% wolumenu", "warn"));
            }

            // 4) Ostatnie ważenie - jak dawno
            var ostatnie = _przefiltrowaneDane.Where(r => r.ActWeight > 0)
                .OrderByDescending(r => r.Data).ThenByDescending(r => r.Godzina).FirstOrDefault();
            if (ostatnie != null)
            {
                var dt = ostatnie.Data.Date.Add(ostatnie.Godzina.TimeOfDay);
                var minutes = (DateTime.Now - dt).TotalMinutes;
                if (minutes >= 0 && minutes < 60 * 24)
                    items.Add(("🕐", $"Ostatnie ważenie {(int)minutes} min temu", "ok"));
            }

            // Rendering
            string overallColor = items.Any(i => i.color == "alert") ? "alert"
                                : items.Any(i => i.color == "warn") ? "warn" : "ok";

            (Color bg, Color border, Color fg, string title) palette = overallColor switch
            {
                "alert" => (Color.FromRgb(0xFE, 0xF2, 0xF2), Color.FromRgb(0xFE, 0xCA, 0xCA), Color.FromRgb(0x99, 0x1B, 0x1B), "Stan: ALERT"),
                "warn"  => (Color.FromRgb(0xFF, 0xFB, 0xEB), Color.FromRgb(0xFD, 0xE6, 0x8A), Color.FromRgb(0x92, 0x40, 0x0E), "Stan: Uwaga"),
                _       => (Color.FromRgb(0xEC, 0xFD, 0xF5), Color.FromRgb(0xA7, 0xF3, 0xD0), Color.FromRgb(0x06, 0x5F, 0x46), "Stan: OK")
            };

            brdHealthInner.Background = new SolidColorBrush(palette.bg);
            brdHealthInner.BorderBrush = new SolidColorBrush(palette.border);
            dotHealth.Fill = new SolidColorBrush(palette.fg);
            txtHealthTitle.Text = palette.title;
            txtHealthTitle.Foreground = new SolidColorBrush(palette.fg);

            icHealthItems.Items.Clear();
            foreach (var (icon, text, _) in items)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 0) };
                sp.Children.Add(new TextBlock { Text = icon + " ", FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(palette.fg) });
                sp.Children.Add(new TextBlock { Text = text, FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(palette.fg), FontWeight = FontWeights.SemiBold });
                icHealthItems.Items.Add(sp);
            }

            pnlHealthStrip.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Context menu (tabela)

        private PrzychodRecord SelectedRecord => dgSzczegoly?.SelectedItem as PrzychodRecord;

        private void CtxFilterOperator_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRecord == null || string.IsNullOrEmpty(SelectedRecord.OperatorID)) return;
            cbOperator.SelectedValue = SelectedRecord.OperatorID;
            EnsureFiltersExpanded();
        }

        private void CtxFilterPartia_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRecord == null || string.IsNullOrEmpty(SelectedRecord.Partia)) return;
            cbPartia.SelectedValue = SelectedRecord.Partia;
            EnsureFiltersExpanded();
        }

        private void CtxFilterTowar_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRecord == null || string.IsNullOrEmpty(SelectedRecord.ArticleID)) return;
            cbTowar.SelectedValue = SelectedRecord.ArticleID;
        }

        private void CtxFilterTerminal_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRecord == null || SelectedRecord.TermID <= 0) return;
            cbTerminal.SelectedValue = SelectedRecord.TermID;
            EnsureFiltersExpanded();
        }

        private void CtxCopyRow_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRecord == null) return;
            var r = SelectedRecord;
            string s = $"{r.Data:yyyy-MM-dd}\t{r.Godzina:HH:mm:ss}\t{r.NazwaTowaru}\t{r.Weight:N2}\t{r.ActWeight:N2}\t{r.Roznica:+0.00;-0.00;0.00}\t{r.Operator}\t{r.Partia}\t{r.Terminal}\t{r.Klasa}";
            try { Clipboard.SetText(s); txtStatus.Text = "Skopiowano wiersz do schowka"; }
            catch { /* niektóre VM-y blokują clipboard */ }
        }

        private void CtxShowPartiaWazenia_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedRecord == null || string.IsNullOrEmpty(SelectedRecord.Partia)) return;
            ShowWazeniaWindow(SelectedRecord.Partia, null);
        }

        #endregion

        #region Tydzień preset

        private void BtnPresetTydzien_Click(object sender, RoutedEventArgs e)
        {
            // Poniedziałek bieżącego tygodnia – dziś
            var today = DateTime.Today;
            int diff = ((int)today.DayOfWeek + 6) % 7; // pon=0, nd=6
            ApplyPreset(today.AddDays(-diff), today);
        }

        #endregion

        #region Export

        private void ExportToCsv()
        {
            if (!_przefiltrowaneDane.Any())
            {
                MessageBox.Show("Brak danych do eksportu.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "Plik CSV|*.csv|Wszystkie pliki|*.*",
                FileName = $"AnalizaPrzychodu_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                using var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8);
                writer.WriteLine("Data;Godzina;Towar;Waga (kg);Operator;Partia;Terminal;Klasa;Tara");
                foreach (var r in _przefiltrowaneDane.OrderBy(r => r.Data).ThenBy(r => r.Godzina))
                {
                    writer.WriteLine($"{r.Data:yyyy-MM-dd};{r.Godzina:HH:mm:ss};{r.NazwaTowaru};{r.ActWeight:N2};{r.Operator};{r.Partia};{r.Terminal};{r.Klasa};{r.Tara:N2}");
                }
                writer.WriteLine();
                writer.WriteLine("=== PODSUMOWANIE ===");
                writer.WriteLine($"Suma kg;{_przefiltrowaneDane.Sum(r => r.ActWeight):N2}");
                writer.WriteLine($"Liczba rekordów;{_przefiltrowaneDane.Count}");
                writer.WriteLine($"Zakres dat;{dpDataOd.SelectedDate:yyyy-MM-dd} - {dpDataDo.SelectedDate:yyyy-MM-dd}");

                MessageBox.Show($"Eksport zakończony.\n\n{dialog.FileName}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        private class FiltrBadge
        {
            public string Etykieta { get; set; }
            public string Wartosc { get; set; }
            public Action ClearAction { get; set; }
        }
    }
}
