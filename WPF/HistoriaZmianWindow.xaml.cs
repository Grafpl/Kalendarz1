using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ClosedXML.Excel;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace Kalendarz1.WPF
{
    public partial class HistoriaZmianWindow : Window
    {
        private readonly string _connLibra;
        private readonly string _connHandel;
        private readonly string _userId;
        private readonly DataTable _dtHistoria = new();
        private bool _isLoading;
        private bool _suppressFilters;
        private bool _isGrouped;
        private HashSet<int> _bookmarks = new();
        private Dictionary<int, List<Kalendarz1.Services.HistoriaZmianMetaService.TagInfo>> _tagsByHistoriaId = new();
        // Avatary handlowców — analogicznie do NoweZamowienieTestWindow (UserHandlowcy table + UserAvatarManager).
        private readonly Dictionary<string, string> _handlowiecMapowanie = new(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BitmapSource> s_handlowiecAvatarCacheHistoria
            = new(StringComparer.OrdinalIgnoreCase);
        private System.Windows.Threading.DispatcherTimer? _autoRefreshTimer;
        private const decimal AnomalyThresholdKg = 500m;
        private DataRow? _selectedRow;
        private Dictionary<int, string> _productNames = new();
        private Dictionary<string, int> _productIdsByName = new(StringComparer.OrdinalIgnoreCase);
        // ZamowienieId → (KodTowaru → Ilosc) — używane do "Filet 100 kg" w kolumnie Towar gdy filtr towaru aktywny
        private Dictionary<int, Dictionary<int, decimal>> _orderItemsByOrder = new();
        private DateTime? _filterDzienUboju;

        // Process-wide cache avatarów — dzielony między wszystkie instancje okna.
        private static readonly ConcurrentDictionary<string, BitmapSource> s_avatarCache
            = new(StringComparer.OrdinalIgnoreCase);

        public HistoriaZmianWindow(string connLibra, string connHandel, string userId = "")
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            _connLibra = connLibra;
            _connHandel = connHandel;
            _userId = userId;

            // Domyślnie dzień uboju = dzisiaj — handlowcy najczęściej oglądają bieżący dzień
            _suppressFilters = true;
            dpDzienUboju.SelectedDate = DateTime.Today;
            _suppressFilters = false;

            InitializeDataTable();
            InitializeKategoriaCombo();
            _ = LoadDataAsync();
        }

        private void InitializeDataTable()
        {
            _dtHistoria.Columns.Add("Id", typeof(int));
            _dtHistoria.Columns.Add("ZamowienieId", typeof(int));
            _dtHistoria.Columns.Add("DataZmiany", typeof(DateTime));
            _dtHistoria.Columns.Add("TypZmiany", typeof(string));          // raw value dla filtra
            _dtHistoria.Columns.Add("TypZmianyDisplay", typeof(string));   // z emoji prefix
            _dtHistoria.Columns.Add("Handlowiec", typeof(string));
            _dtHistoria.Columns.Add("Odbiorca", typeof(string));
            _dtHistoria.Columns.Add("UzytkownikNazwa", typeof(string));
            _dtHistoria.Columns.Add("UzytkownikId", typeof(string));
            _dtHistoria.Columns.Add("Towar", typeof(string));            // oryginalny (z wpisu historii)
            _dtHistoria.Columns.Add("TowarPokazany", typeof(string));    // dynamiczny — pokazywany w DataGrid
            _dtHistoria.Columns.Add("KodTowaru", typeof(int));
            _dtHistoria.Columns.Add("WszystkieTowary", typeof(string));  // ";id1;id2;id3;" dla filtra po pozycjach
            _dtHistoria.Columns.Add("OpisZmiany", typeof(string));
            _dtHistoria.Columns.Add("DataUboju", typeof(DateTime));
            _dtHistoria.Columns.Add("SearchAll", typeof(string));
            // Structured: kategoria zmiany + przed/po dla łatwej lokalizacji zmian kg/cen/dat itd.
            _dtHistoria.Columns.Add("Kategoria", typeof(string));          // główna kategoria (KG / CENA / TERMIN / ...)
            _dtHistoria.Columns.Add("KategoriaSet", typeof(string));       // multi-tag ";KG;UTWORZENIE;" dla utworzeń pozycji
            _dtHistoria.Columns.Add("PoleZmienione", typeof(string));
            _dtHistoria.Columns.Add("WartoscPoprzednia", typeof(string));
            _dtHistoria.Columns.Add("WartoscNowa", typeof(string));
            _dtHistoria.Columns.Add("ZmianaDisplay", typeof(string));      // tekstowy fallback (np. Excel export)
            // Kolumny do 3-element template: "przed (gray) ↑ (color) po (color)"
            _dtHistoria.Columns.Add("ZmianaPrzed", typeof(string));
            _dtHistoria.Columns.Add("ZmianaArrow", typeof(string));        // "↑" / "↓" / "→"
            _dtHistoria.Columns.Add("ZmianaArrowBrush", typeof(System.Windows.Media.Brush));
            _dtHistoria.Columns.Add("ZmianaPo", typeof(string));
            _dtHistoria.Columns.Add("TowarImage", typeof(ImageSource));    // miniatura towaru z TowaryZdjeciaService
            _dtHistoria.Columns.Add("DataZmianyDisplay", typeof(string));  // z dniem tygodnia
            _dtHistoria.Columns.Add("DataUbojuDisplay", typeof(string));   // z dniem tygodnia
            _dtHistoria.Columns.Add("IsAnomalia", typeof(bool));           // |delta kg| > 500
            _dtHistoria.Columns.Add("HandlowiecAvatar", typeof(ImageSource));  // prawdziwy avatar z UserAvatarManager

            _dtHistoria.DefaultView.Sort = "DataZmiany DESC";
            UpdateItemsSource();
            SetupDataGrid();
        }

        // Ustawia ItemsSource w zależności od trybu:
        //   - tryb płaski: DataView z RowFilter (jak teraz)
        //   - tryb grupowany: ListCollectionView<DataRowView> z GroupDescriptions po ZamowienieId
        private void UpdateItemsSource()
        {
            if (_isGrouped)
            {
                var rows = _dtHistoria.DefaultView.Cast<DataRowView>().ToList();
                var lcv = new ListCollectionView(rows);
                lcv.GroupDescriptions.Add(new DataRowViewGroupDescription("ZamowienieId"));
                // Sort: po zamówieniu DESC (najnowsze pierwszy), wewnątrz grupy po Towar ASC + DataZmiany DESC
                lcv.CustomSort = new DataRowViewMultiSort(
                    ("ZamowienieId", false),
                    ("Towar", true),
                    ("DataZmiany", false));
                dgHistoria.ItemsSource = lcv;
            }
            else
            {
                dgHistoria.ItemsSource = _dtHistoria.DefaultView;
            }

            // Empty state
            int count = _dtHistoria.DefaultView.Count;
            if (EmptyStatePanel != null)
            {
                EmptyStatePanel.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
                if (count == 0)
                {
                    string hint = _filterDzienUboju.HasValue
                        ? $"Brak zmian dla dnia uboju {_filterDzienUboju.Value:dd.MM.yyyy} z bieżącymi filtrami."
                        : "Wybierz dzień uboju lub wyczyść filtry.";
                    EmptyStateHint.Text = hint;
                }
            }
        }

        private void SetupDataGrid()
        {
            dgHistoria.Columns.Clear();
            dgHistoria.FrozenColumnCount = 2;   // Data uboju + Data zmiany zawsze widoczne przy scrolu
            dgHistoria.SelectionChanged -= DgHistoria_SelectionChanged;
            dgHistoria.SelectionChanged += DgHistoria_SelectionChanged;

            // Sergiusz: kolejność Data uboju → Data zmiany → Użytkownik → reszta
            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Data uboju",
                Binding = new Binding("DataUbojuDisplay"),
                Width = new DataGridLength(130),
                SortMemberPath = "DataUboju"
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Data zmiany",
                Binding = new Binding("DataZmianyDisplay"),
                Width = new DataGridLength(150),
                SortMemberPath = "DataZmiany"
            });

            // Kolumna Użytkownik z avatarem (DataGridTemplateColumn)
            var userColumn = new DataGridTemplateColumn
            {
                Header = "Użytkownik",
                Width = new DataGridLength(140),
                SortMemberPath = "UzytkownikNazwa"
            };

            var cellTemplate = new DataTemplate();
            var stackPanelFactory = new FrameworkElementFactory(typeof(StackPanel));
            stackPanelFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            stackPanelFactory.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(Grid.WidthProperty, 26.0);
            gridFactory.SetValue(Grid.HeightProperty, 26.0);
            gridFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0));

            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.WidthProperty, 26.0);
            borderFactory.SetValue(Border.HeightProperty, 26.0);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
            borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(70, 104, 44)));
            borderFactory.SetValue(FrameworkElement.NameProperty, "avatarBorder");

            var initialsFactory = new FrameworkElementFactory(typeof(TextBlock));
            initialsFactory.SetBinding(TextBlock.TextProperty, new Binding("UzytkownikNazwa") { Converter = new InitialsConverter() });
            initialsFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            initialsFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            initialsFactory.SetValue(TextBlock.ForegroundProperty, Brushes.White);
            initialsFactory.SetValue(TextBlock.FontSizeProperty, 10.0);
            initialsFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            borderFactory.AppendChild(initialsFactory);

            var ellipseFactory = new FrameworkElementFactory(typeof(Ellipse));
            ellipseFactory.SetValue(Ellipse.WidthProperty, 26.0);
            ellipseFactory.SetValue(Ellipse.HeightProperty, 26.0);
            ellipseFactory.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
            ellipseFactory.SetValue(FrameworkElement.NameProperty, "avatarImage");

            gridFactory.AppendChild(borderFactory);
            gridFactory.AppendChild(ellipseFactory);

            var nameFactory = new FrameworkElementFactory(typeof(TextBlock));
            nameFactory.SetBinding(TextBlock.TextProperty, new Binding("UzytkownikNazwa"));
            nameFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            nameFactory.SetValue(TextBlock.FontSizeProperty, 11.0);

            stackPanelFactory.AppendChild(gridFactory);
            stackPanelFactory.AppendChild(nameFactory);

            cellTemplate.VisualTree = stackPanelFactory;
            userColumn.CellTemplate = cellTemplate;

            dgHistoria.Columns.Add(userColumn);
            dgHistoria.LoadingRow += DgHistoria_LoadingRow;

            // Typ, Odbiorca, Handlowiec — po Użytkowniku (Sergiusz: "później osoba która zmieniała")
            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Typ",
                Binding = new Binding("TypZmianyDisplay"),
                Width = new DataGridLength(135),
                SortMemberPath = "TypZmiany"
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Odbiorca",
                Binding = new Binding("Odbiorca"),
                Width = new DataGridLength(170)
            });

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Handlowiec",
                Binding = new Binding("Handlowiec"),
                Width = new DataGridLength(95)
            });

            // Towar z miniaturą (BLOB z TowaryZdjeciaService) + nazwa
            var towarCol = new DataGridTemplateColumn
            {
                Header = "Towar",
                Width = new DataGridLength(190),
                SortMemberPath = "TowarPokazany"
            };
            var towarTmpl = new DataTemplate();
            var towarSp = new FrameworkElementFactory(typeof(StackPanel));
            towarSp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            towarSp.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            // Miniaturka 34x34 — Border z Image, Stretch=Uniform żeby cały obraz był widoczny (bez przycinania)
            var imgBorder = new FrameworkElementFactory(typeof(Border));
            imgBorder.SetValue(Border.WidthProperty, 34.0);
            imgBorder.SetValue(Border.HeightProperty, 34.0);
            imgBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            imgBorder.SetValue(Border.MarginProperty, new Thickness(0, 0, 8, 0));
            imgBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)));
            imgBorder.SetValue(Border.ClipToBoundsProperty, true);

            var imgEl = new FrameworkElementFactory(typeof(Image));
            imgEl.SetBinding(Image.SourceProperty, new Binding("TowarImage"));
            imgEl.SetValue(Image.StretchProperty, Stretch.Uniform);   // cały obraz widoczny (nie przycina)
            imgBorder.AppendChild(imgEl);
            towarSp.AppendChild(imgBorder);

            var towarTb = new FrameworkElementFactory(typeof(TextBlock));
            towarTb.SetBinding(TextBlock.TextProperty, new Binding("TowarPokazany"));
            towarTb.SetValue(TextBlock.FontSizeProperty, 12.0);
            towarTb.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            towarTb.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            towarTb.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            towarSp.AppendChild(towarTb);

            towarTmpl.VisualTree = towarSp;
            towarCol.CellTemplate = towarTmpl;
            dgHistoria.Columns.Add(towarCol);

            // Kluczowa kolumna Zmiana — template z 3 elementami: przed (szary) ↑↓→ (kolor) po (kolor matching).
            // Zielona strzałka ↑ = wzrost, czerwona ↓ = spadek, szara → = neutralna/tekstowa.
            var zmianaColumn = new DataGridTemplateColumn
            {
                Header = "Zmiana (przed → po)",
                Width = new DataGridLength(260),
                SortMemberPath = "ZmianaPrzed"
            };
            var tmpl = new DataTemplate();
            var sp = new FrameworkElementFactory(typeof(StackPanel));
            sp.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            sp.SetValue(StackPanel.VerticalAlignmentProperty, VerticalAlignment.Center);

            var tbPrzed = new FrameworkElementFactory(typeof(TextBlock));
            tbPrzed.SetBinding(TextBlock.TextProperty, new Binding("ZmianaPrzed"));
            tbPrzed.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)));
            tbPrzed.SetValue(TextBlock.FontSizeProperty, 12.0);
            tbPrzed.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
            tbPrzed.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            // Skreślona — jasne że to wartość PRZED zmianą (zastąpiona)
            tbPrzed.SetValue(TextBlock.TextDecorationsProperty, TextDecorations.Strikethrough);

            var tbArrow = new FrameworkElementFactory(typeof(TextBlock));
            tbArrow.SetBinding(TextBlock.TextProperty, new Binding("ZmianaArrow"));
            tbArrow.SetBinding(TextBlock.ForegroundProperty, new Binding("ZmianaArrowBrush"));
            tbArrow.SetValue(TextBlock.FontSizeProperty, 18.0);
            tbArrow.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            tbArrow.SetValue(TextBlock.MarginProperty, new Thickness(10, 0, 10, 0));
            tbArrow.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            var tbPo = new FrameworkElementFactory(typeof(TextBlock));
            tbPo.SetBinding(TextBlock.TextProperty, new Binding("ZmianaPo"));
            tbPo.SetBinding(TextBlock.ForegroundProperty, new Binding("ZmianaArrowBrush"));
            tbPo.SetValue(TextBlock.FontSizeProperty, 13.0);
            tbPo.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            tbPo.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);

            sp.AppendChild(tbPrzed);
            sp.AppendChild(tbArrow);
            sp.AppendChild(tbPo);
            tmpl.VisualTree = sp;
            zmianaColumn.CellTemplate = tmpl;
            dgHistoria.Columns.Add(zmianaColumn);

            dgHistoria.Columns.Add(new DataGridTextColumn
            {
                Header = "Opis zmiany",
                Binding = new Binding("OpisZmiany"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });
        }

        // ════════════════════ LOAD DATA ════════════════════

        private async Task LoadDataAsync()
        {
            if (_isLoading) return;
            _isLoading = true;
            ShowLoader(true, "Pobieranie danych z bazy…");

            try
            {
                _filterDzienUboju = dpDzienUboju.SelectedDate?.Date;

                _dtHistoria.Rows.Clear();
                _orderItemsByOrder.Clear();
                _productNames.Clear();
                _productIdsByName.Clear();

                // 4 niezależne loady równolegle (różne DB, brak współdzielonego stanu)
                var tContractors = LoadContractorsAsync();                                          // HANDEL
                var tProducts = LoadProductsAsync();                                                // HANDEL
                var tOrdersAndItems = LoadOrdersAndItemsAsync();                                    // LibraNet
                var tHistory = LoadHistoryRawAsync(_filterDzienUboju);                              // LibraNet
                var tBookmarks = Kalendarz1.Services.HistoriaZmianMetaService.LoadBookmarksAsync(_userId);
                var tTags = Kalendarz1.Services.HistoriaZmianMetaService.LoadAllTagsAsync();
                // TowaryZdjecia — czekamy żeby miniatury były gotowe przy build wierszy (cache 15-min między oknami i tak szybko)
                var tZdjecia = Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.LoadAsync(_connLibra);
                // Mapowanie handlowiec → UserID z UserHandlowcy — żeby pobrać prawdziwe avatary
                var tUserHandlowcy = LoadUserHandlowcyAsync();

                await Task.WhenAll(tContractors, tProducts, tOrdersAndItems, tHistory, tBookmarks, tTags, tZdjecia, tUserHandlowcy);

                _bookmarks = await tBookmarks;
                _tagsByHistoriaId = await tTags;

                var contractors = await tContractors;
                _productNames = await tProducts;

                // Pre-cache avatarów handlowców — UserAvatarManager pobiera raz na sesję, freeze BitmapSource
                var uniqueHandlowcy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var contr in contractors.Values)
                    if (!string.IsNullOrWhiteSpace(contr.Salesman)) uniqueHandlowcy.Add(contr.Salesman);
                foreach (var handlName in uniqueHandlowcy)
                {
                    try { EnsureHandlowiecAvatarCachedHistoria(handlName); } catch { }
                }
                _productIdsByName = _productNames.GroupBy(kv => kv.Value)
                                                 .ToDictionary(g => g.Key, g => g.First().Key,
                                                               StringComparer.OrdinalIgnoreCase);
                var (orderToClient, orderToDataUboju, orderItems) = await tOrdersAndItems;
                _orderItemsByOrder = orderItems;
                var historyRows = await tHistory;

                // Build DataTable — BeginLoadData wyłącza notyfikacje + constraints check, EndLoadData restore.
                // Sort wyłączony podczas Add (sortuje zwykle PO każdym wierszu — kosztowne dla N=5000).
                string savedSort = _dtHistoria.DefaultView.Sort;
                _dtHistoria.DefaultView.Sort = "";
                _dtHistoria.BeginLoadData();
                foreach (var h in historyRows)
                {
                    // Heurystyka legacy: jeśli structured fields puste → wyciągnij z OpisZmiany.
                    // Łapie wpisy typu "(brak) > Filet A 3000 kg", "Dodano: Filet 100 kg" itp.
                    EnrichLegacyEntry(h);

                    string towar = "";
                    int kodTowaru = h.KodTowaru;
                    if (kodTowaru > 0 && _productNames.TryGetValue(kodTowaru, out var nameByKod))
                        towar = nameByKod;
                    else
                    {
                        // Spróbuj wyciągnąć z PoleZmienione "Pozycja: NazwaTowaru - Atrybut"
                        string fromPole = ExtractTowarFromPoleZmienione(h.PoleZmienione);
                        if (!string.IsNullOrEmpty(fromPole))
                        {
                            towar = fromPole;
                            // Reverse lookup → KodTowaru żeby filter towaru działał
                            if (_productIdsByName.TryGetValue(fromPole, out int kodFromName))
                                kodTowaru = kodFromName;
                        }
                        else if (kodTowaru == 0)
                        {
                            towar = ExtractProductFromDescription(h.OpisZmiany);
                        }
                    }
                    // ── Cleanup: jeśli "towar" zawiera szum ("(brak)", ">", "kg" itp.), wyciągnij realną nazwę
                    //    przez fuzzy match z _productNames (catalog towarów z bazy HANDEL).
                    towar = CleanTowarName(towar, kodTowaru);
                    if (kodTowaru == 0 && !string.IsNullOrEmpty(towar)
                        && _productIdsByName.TryGetValue(towar, out int kodFromClean))
                        kodTowaru = kodFromClean;

                    string handlowiec = "";
                    string odbiorca = "";
                    if (orderToClient.TryGetValue(h.ZamowienieId, out int clientId) &&
                        contractors.TryGetValue(clientId, out var contr))
                    {
                        handlowiec = contr.Salesman;
                        odbiorca = contr.Name;
                    }

                    DateTime? dataUboju = orderToDataUboju.TryGetValue(h.ZamowienieId, out var du) ? du : null;

                    // WszystkieTowary — semikolonami otoczona lista IDs do filtra LIKE
                    string wszystkieTowary = "";
                    if (_orderItemsByOrder.TryGetValue(h.ZamowienieId, out var orderTowary) && orderTowary.Count > 0)
                        wszystkieTowary = ";" + string.Join(";", orderTowary.Keys) + ";";
                    else if (kodTowaru > 0)
                        wszystkieTowary = $";{kodTowaru};";

                    string typDisplay = TypIcon(h.TypZmiany) + " " + h.TypZmiany;
                    string kategoria = Categorize(h.TypZmiany, h.PoleZmienione, h.OpisZmiany, h.WartoscNowa, h.WartoscPoprzednia);
                    var (zmPrzed, zmArrow, zmBrush, zmPo, zmianaDisplay) = BuildZmiana(kategoria, h.WartoscPoprzednia, h.WartoscNowa, h.OpisZmiany);

                    // Heurystyka UTWORZENIE / USUNIECIE pozycji (Sergiusz: "z niczego weszło — nie edycja")
                    string kategoriaSet = $";{kategoria};";
                    string typEff = h.TypZmiany;
                    string typDisplayEff = typDisplay;
                    if (kategoria == KatKg && (h.PoleZmienione ?? "").StartsWith("Pozycja:", StringComparison.OrdinalIgnoreCase)
                        && TryParseNumber(h.WartoscPoprzednia ?? "", out decimal sNumT)
                        && TryParseNumber(h.WartoscNowa ?? "", out decimal nNumT))
                    {
                        if (sNumT == 0 && nNumT > 0)
                        {
                            typEff = "UTWORZENIE";
                            typDisplayEff = "➕ UTWORZENIE pozycji";
                            kategoriaSet = ";KG;UTWORZENIE;";
                        }
                        else if (sNumT > 0 && nNumT == 0)
                        {
                            typEff = "USUNIECIE";
                            typDisplayEff = "🗑 USUNIECIE pozycji";
                            kategoriaSet = ";KG;USUNIECIE;";
                        }
                    }
                    string searchAll = string.Join(" | ", new[] {
                        h.OpisZmiany ?? "", odbiorca, handlowiec, h.UzytkownikNazwa ?? "", towar,
                        h.ZamowienieId.ToString(), h.PoleZmienione ?? "", h.WartoscPoprzednia ?? "", h.WartoscNowa ?? ""
                    });

                    // Zdjęcie towaru (BLOB z TowaryZdjeciaService — cache 15 min między oknami)
                    ImageSource? towarImg = kodTowaru > 0
                        ? Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.Get(kodTowaru)
                        : null;
                    string dataZmianyDisp = FormatPlDate(h.DataZmiany, true);                       // "Pn 09.05.2026 14:32"
                    string dataUbojuDisp = dataUboju.HasValue ? FormatPlDate(dataUboju.Value, false) : ""; // "Pt 10.05.2026"

                    // Anomalia: zmiana ilości kg powyżej progu — handlowiec namnożył lub usunął dużo
                    bool isAnomalia = kategoria == KatKg
                        && TryParseNumber(h.WartoscPoprzednia ?? "", out decimal sNum)
                        && TryParseNumber(h.WartoscNowa ?? "", out decimal nNum)
                        && Math.Abs(nNum - sNum) >= AnomalyThresholdKg;

                    // Avatar handlowca z cache (UserAvatarManager — prawdziwy z sieci albo wygenerowany)
                    BitmapSource? handlAvatar = null;
                    if (!string.IsNullOrEmpty(handlowiec))
                        s_handlowiecAvatarCacheHistoria.TryGetValue(handlowiec, out handlAvatar);

                    _dtHistoria.Rows.Add(
                        h.Id, h.ZamowienieId, h.DataZmiany, typEff, typDisplayEff,
                        handlowiec, odbiorca, h.UzytkownikNazwa, h.UzytkownikId,
                        towar, towar, kodTowaru,                            // Towar (oryg) + TowarPokazany + KodTowaru
                        wszystkieTowary, h.OpisZmiany,
                        dataUboju.HasValue ? (object)dataUboju.Value : DBNull.Value,
                        searchAll,
                        kategoria, kategoriaSet, h.PoleZmienione, h.WartoscPoprzednia, h.WartoscNowa, zmianaDisplay,
                        zmPrzed, zmArrow, zmBrush, zmPo,
                        (object?)towarImg ?? DBNull.Value,
                        dataZmianyDisp, dataUbojuDisp,
                        isAnomalia,
                        (object?)handlAvatar ?? DBNull.Value);
                }

                // Synthesize wpisy pozycji dla legacy UTWORZEŃ:
                // Dla każdego wpisu UTWORZENIE bez istniejących per-pozycja wpisów,
                // generujemy pseudo-wpisy "Pozycja: X - Zam." 0 → ilosc używając _orderItemsByOrder.
                AddSyntheticUtworzeniePozycji();
                _dtHistoria.EndLoadData();
                _dtHistoria.DefaultView.Sort = string.IsNullOrEmpty(savedSort) ? "DataZmiany DESC" : savedSort;

                string zakresInfo = _filterDzienUboju.HasValue
                    ? $"Dzień uboju: {_filterDzienUboju.Value:dd.MM.yyyy}"
                    : "Wszystkie ostatnie 5000 zmian";
                txtDateRange.Text = $"{zakresInfo}  ·  {_dtHistoria.Rows.Count:N0} zmian";
                _suppressFilters = true;
                PopulateFilterComboBoxes();
                // Sergiusz: zawsze startuj z Odbiorca = (Wszystkie) po reload
                if (cmbOdbiorca?.Items.Count > 0) cmbOdbiorca.SelectedIndex = 0;
                _suppressFilters = false;
                ApplyFilters();
                UpdateDashboardCounters();

                // Pre-load avatarów w tle (static cache na całą sesję)
                _ = Task.Run(PreloadAvatars);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isLoading = false;
                ShowLoader(false);
            }
        }

        // Mapowanie handlowiec → UserID z tabeli UserHandlowcy (LibraNet) — używane do pobrania avatarów.
        private async Task LoadUserHandlowcyAsync()
        {
            _handlowiecMapowanie.Clear();
            try
            {
                await using var cn = new SqlConnection(_connLibra);
                await cn.OpenAsync();
                await using var cmd = new SqlCommand("SELECT HandlowiecName, UserID FROM UserHandlowcy", cn);
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    string? h = rd["HandlowiecName"]?.ToString();
                    string? uid = rd["UserID"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(h) && !string.IsNullOrWhiteSpace(uid))
                        _handlowiecMapowanie[h!] = uid!;
                }
            }
            catch { }
        }

        // Ładuje prawdziwy avatar handlowca (z UserAvatarManager + sieciowej ścieżki avatarów) — kopia z NoweZamowienieTestWindow.
        private void EnsureHandlowiecAvatarCachedHistoria(string handlowiec, int size = 48)
        {
            if (string.IsNullOrWhiteSpace(handlowiec)) return;
            if (s_handlowiecAvatarCacheHistoria.ContainsKey(handlowiec)) return;

            BitmapSource? bmp = null;
            if (_handlowiecMapowanie.TryGetValue(handlowiec, out var uid))
            {
                try
                {
                    if (UserAvatarManager.HasAvatar(uid))
                        using (var av = UserAvatarManager.GetAvatarRounded(uid, size))
                            if (av != null) bmp = ConvertGdiToBitmapSource(av);
                    if (bmp == null)
                        using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, uid, size))
                            bmp = ConvertGdiToBitmapSource(defAv);
                }
                catch { }
            }
            if (bmp == null)
            {
                try
                {
                    using (var defAv = UserAvatarManager.GenerateDefaultAvatar(handlowiec, handlowiec, size))
                        bmp = ConvertGdiToBitmapSource(defAv);
                }
                catch { }
            }
            if (bmp != null)
            {
                bmp.Freeze();
                s_handlowiecAvatarCacheHistoria[handlowiec] = bmp;
            }
        }

        // Konwersja GDI Image → BitmapSource przez HBitmap (analogicznie do NoweZamowienieTestWindow)
        private static BitmapSource? ConvertGdiToBitmapSource(System.Drawing.Image image)
        {
            if (image == null) return null;
            using var bitmap = new System.Drawing.Bitmap(image);
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                DeleteHistoriaGdiObject(hBitmap);
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool DeleteHistoriaGdiObject(IntPtr hObject);

        private async Task<Dictionary<int, (string Name, string Salesman)>> LoadContractorsAsync()
        {
            var result = new Dictionary<int, (string Name, string Salesman)>();
            await using var cn = new SqlConnection(_connHandel);
            await cn.OpenAsync();
            const string sql = @"SELECT c.Id, c.Shortcut, wym.CDim_Handlowiec_Val
                                 FROM [HANDEL].[SSCommon].[STContractors] c
                                 LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] wym
                                 ON c.Id = wym.ElementId";
            await using var cmd = new SqlCommand(sql, cn);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                int id = rd.GetInt32(0);
                string shortcut = rd.IsDBNull(1) ? "" : rd.GetString(1);
                string salesman = rd.IsDBNull(2) ? "" : rd.GetString(2);
                result[id] = (string.IsNullOrWhiteSpace(shortcut) ? $"KH {id}" : shortcut, salesman);
            }
            return result;
        }

        private async Task<Dictionary<int, string>> LoadProductsAsync()
        {
            var result = new Dictionary<int, string>();
            try
            {
                await using var cn = new SqlConnection(_connHandel);
                await cn.OpenAsync();
                const string sql = "SELECT ID, CAST(kod AS NVARCHAR(64)) AS kod FROM [HANDEL].[HM].[TW] WHERE katalog IN (67095, 67153) ORDER BY kod";
                await using var cmd = new SqlCommand(sql, cn);
                await using var rdr = await cmd.ExecuteReaderAsync();
                while (await rdr.ReadAsync())
                {
                    if (!rdr.IsDBNull(0))
                    {
                        int id = rdr.GetInt32(0);
                        string kod = rdr.IsDBNull(1) ? $"ID:{id}" : rdr.GetString(1);
                        result[id] = kod;
                    }
                }
            }
            catch { /* fallback do pustego dict */ }
            return result;
        }

        private async Task<(Dictionary<int, int>, Dictionary<int, DateTime>, Dictionary<int, Dictionary<int, decimal>>)>
            LoadOrdersAndItemsAsync()
        {
            var orderToClient = new Dictionary<int, int>();
            var orderToDataUboju = new Dictionary<int, DateTime>();
            var orderItems = new Dictionary<int, Dictionary<int, decimal>>();

            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            // Czy istnieje kolumna DataUboju
            bool hasDataUboju;
            await using (var c = new SqlCommand(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='ZamowieniaMieso' AND COLUMN_NAME='DataUboju'", cn))
            {
                hasDataUboju = Convert.ToInt32(await c.ExecuteScalarAsync()) > 0;
            }

            // Zamówienia — wszystkie (potrzebne do mapowania klient + DataUboju)
            string sqlOrders = hasDataUboju
                ? "SELECT Id, KlientId, DataUboju FROM dbo.ZamowieniaMieso"
                : "SELECT Id, KlientId FROM dbo.ZamowieniaMieso";
            await using (var cmd = new SqlCommand(sqlOrders, cn))
            await using (var rd = await cmd.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    int orderId = rd.GetInt32(0);
                    int clientId = rd.IsDBNull(1) ? 0 : rd.GetInt32(1);
                    orderToClient[orderId] = clientId;

                    if (hasDataUboju && !rd.IsDBNull(2))
                        orderToDataUboju[orderId] = rd.GetDateTime(2);
                }
            }

            // Pozycje zamówień (KodTowaru + ilość per zamówienie) — używane do filtra towaru
            // i do pokazania "Filet 100 kg" w kolumnie Towar gdy filtr towaru aktywny.
            const string sqlItems = "SELECT ZamowienieId, KodTowaru, Ilosc FROM dbo.ZamowieniaMiesoTowar WHERE KodTowaru IS NOT NULL";
            await using (var cmd = new SqlCommand(sqlItems, cn))
            await using (var rd = await cmd.ExecuteReaderAsync())
            {
                while (await rd.ReadAsync())
                {
                    int orderId = rd.GetInt32(0);
                    if (!int.TryParse(rd[1]?.ToString(), out int kod)) continue;
                    decimal ilosc = rd.IsDBNull(2) ? 0m : Convert.ToDecimal(rd.GetValue(2));
                    if (!orderItems.TryGetValue(orderId, out var map))
                    {
                        map = new Dictionary<int, decimal>();
                        orderItems[orderId] = map;
                    }
                    // jeśli ten sam towar występuje wielokrotnie w pozycjach — sumuj
                    if (map.ContainsKey(kod)) map[kod] += ilosc;
                    else map[kod] = ilosc;
                }
            }

            return (orderToClient, orderToDataUboju, orderItems);
        }

        private class HistoryRecord
        {
            public int Id, ZamowienieId, KodTowaru;
            public DateTime DataZmiany;
            public string TypZmiany = "", UzytkownikNazwa = "", OpisZmiany = "", UzytkownikId = "";
            public string PoleZmienione = "", WartoscPoprzednia = "", WartoscNowa = "";
        }

        private async Task<List<HistoryRecord>> LoadHistoryRawAsync(DateTime? dzienUboju)
        {
            var list = new List<HistoryRecord>();
            await using var cn = new SqlConnection(_connLibra);
            await cn.OpenAsync();

            // Czy istnieje tabela
            bool tableExists;
            await using (var c = new SqlCommand(
                "SELECT COUNT(*) FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[HistoriaZmianZamowien]') AND type='U'", cn))
            {
                tableExists = Convert.ToInt32(await c.ExecuteScalarAsync()) > 0;
            }
            if (!tableExists) return list;

            // Czy kolumna KodTowaru w historii
            bool hasKodTowaru;
            await using (var c = new SqlCommand(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='HistoriaZmianZamowien' AND COLUMN_NAME='KodTowaru'", cn))
            {
                hasKodTowaru = Convert.ToInt32(await c.ExecuteScalarAsync()) > 0;
            }

            // Structured zmiany — PoleZmienione + WartoscPoprzednia + WartoscNowa
            // (kolumny istnieją w schemacie utworzonym przez HistoriaZmianService.EnsureTableExistsAsync).
            string structCols = ", h.PoleZmienione, h.WartoscPoprzednia, h.WartoscNowa";

            // Filtr dnia uboju: jeśli wybrany — JOIN z ZamowieniaMieso po DataUboju. Inaczej — TOP 5000 ostatnich.
            string kolumny = hasKodTowaru
                ? "h.Id, h.ZamowienieId, h.DataZmiany, h.TypZmiany, h.UzytkownikNazwa, h.OpisZmiany, h.KodTowaru, h.Uzytkownik" + structCols
                : "h.Id, h.ZamowienieId, h.DataZmiany, h.TypZmiany, h.UzytkownikNazwa, h.OpisZmiany, h.Uzytkownik" + structCols;

            string sql;
            if (dzienUboju.HasValue)
            {
                sql = $@"SELECT TOP 5000 {kolumny}
                         FROM dbo.HistoriaZmianZamowien h
                         INNER JOIN dbo.ZamowieniaMieso z ON z.Id = h.ZamowienieId
                         WHERE z.DataUboju IS NOT NULL AND CAST(z.DataUboju AS DATE) = @dzien
                         ORDER BY h.DataZmiany DESC";
            }
            else
            {
                sql = $@"SELECT TOP 5000 {kolumny}
                         FROM dbo.HistoriaZmianZamowien h
                         ORDER BY h.DataZmiany DESC";
            }

            await using var cmd = new SqlCommand(sql, cn);
            if (dzienUboju.HasValue) cmd.Parameters.AddWithValue("@dzien", dzienUboju.Value.Date);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
            {
                var rec = new HistoryRecord
                {
                    Id = rdr.GetInt32(0),
                    ZamowienieId = rdr.GetInt32(1),
                    DataZmiany = rdr.GetDateTime(2),
                    TypZmiany = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                    UzytkownikNazwa = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                    OpisZmiany = rdr.IsDBNull(5) ? "" : rdr.GetString(5)
                };
                int idxStruct;
                if (hasKodTowaru)
                {
                    rec.KodTowaru = rdr.IsDBNull(6) ? 0 : rdr.GetInt32(6);
                    rec.UzytkownikId = rdr.IsDBNull(7) ? "" : rdr.GetString(7);
                    idxStruct = 8;
                }
                else
                {
                    rec.UzytkownikId = rdr.IsDBNull(6) ? "" : rdr.GetString(6);
                    idxStruct = 7;
                }
                // PoleZmienione + WartoscPoprzednia + WartoscNowa (3 kolejne kolumny)
                if (rdr.FieldCount > idxStruct)
                {
                    rec.PoleZmienione = rdr.IsDBNull(idxStruct) ? "" : rdr.GetString(idxStruct);
                    rec.WartoscPoprzednia = rdr.IsDBNull(idxStruct + 1) ? "" : rdr.GetString(idxStruct + 1);
                    rec.WartoscNowa = rdr.IsDBNull(idxStruct + 2) ? "" : rdr.GetString(idxStruct + 2);
                }
                list.Add(rec);
            }
            return list;
        }

        // Generuje syntetyczne wpisy "Pozycja: X - Zam. (0 → ilość)" dla legacy UTWORZEŃ
        // które nie zostały zalogowane per-pozycja (sprzed implementacji LogujDodaniePozycji).
        // Dzięki temu filtr Towar = X łapie też utworzenie tego towaru w zamówieniu.
        private void AddSyntheticUtworzeniePozycji()
        {
            // Znajdź zamówienia gdzie jest wpis UTWORZENIE ale BRAK wpisów typu "Pozycja: ... - Zam."
            var utworzenia = new Dictionary<int, DataRow>();
            var maPozycjeZam = new HashSet<int>();

            foreach (DataRow r in _dtHistoria.Rows)
            {
                int zamId = Convert.ToInt32(r["ZamowienieId"]);
                string typ = r["TypZmiany"]?.ToString() ?? "";
                string pole = r["PoleZmienione"]?.ToString() ?? "";

                if (typ == "UTWORZENIE" && !utworzenia.ContainsKey(zamId))
                    utworzenia[zamId] = r;

                if (pole.StartsWith("Pozycja:", StringComparison.OrdinalIgnoreCase)
                    && pole.IndexOf("- Zam.", StringComparison.OrdinalIgnoreCase) > 0)
                    maPozycjeZam.Add(zamId);
            }

            foreach (var kvp in utworzenia)
            {
                int zamId = kvp.Key;
                if (maPozycjeZam.Contains(zamId)) continue;
                if (!_orderItemsByOrder.TryGetValue(zamId, out var pozycje) || pozycje.Count == 0) continue;

                DataRow utw = kvp.Value;
                DateTime dataZmiany = (DateTime)utw["DataZmiany"];
                string handlowiec = utw["Handlowiec"]?.ToString() ?? "";
                string odbiorca = utw["Odbiorca"]?.ToString() ?? "";
                string uzytkownik = utw["UzytkownikNazwa"]?.ToString() ?? "";
                string uzytkownikId = utw["UzytkownikId"]?.ToString() ?? "";
                object dataUboju = utw["DataUboju"];

                int offset = 1;
                foreach (var pkv in pozycje.OrderByDescending(x => x.Value))   // największe pozycje na górze
                {
                    int kod = pkv.Key;
                    decimal ilosc = pkv.Value;
                    if (ilosc <= 0) continue;
                    string nazwa = _productNames.TryGetValue(kod, out var n) ? n : $"Towar {kod}";
                    string pole = $"Pozycja: {nazwa} - Zam.";
                    string wartoscNowa = ilosc.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    var (zmPrzed, zmArrow, zmBrush, zmPo, zmianaDisp) = BuildZmiana(KatKg, "0", wartoscNowa, null);
                    string opisSynth = $"(pozycja UTWORZENIA) {nazwa} {ilosc:N0} kg";
                    string searchAll = string.Join(" | ", new[] {
                        opisSynth, odbiorca, handlowiec, uzytkownik, nazwa, zamId.ToString(), pole, "0", wartoscNowa
                    });
                    // Wpis lekko po UTWORZENIU (+1, +2 sekundy) żeby sort DataZmiany DESC pokazywał logicznie
                    DateTime dt = dataZmiany.AddSeconds(offset++);
                    string wszystkieTow = ";" + string.Join(";", pozycje.Keys) + ";";

                    ImageSource? synthImg = Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.Get(kod);
                    string synthDataZmiany = FormatPlDate(dt, true);
                    DateTime? duDt = dataUboju is DateTime dtu ? dtu : (DateTime?)null;
                    string synthDataUboju = duDt.HasValue ? FormatPlDate(duDt.Value, false) : "";

                    BitmapSource? synthHandlAvatar = null;
                    if (!string.IsNullOrEmpty(handlowiec))
                        s_handlowiecAvatarCacheHistoria.TryGetValue(handlowiec, out synthHandlAvatar);

                    _dtHistoria.Rows.Add(
                        -zamId * 1000 - offset,            // syntetyczne unikalne id (ujemne, nie koliduje z bazą)
                        zamId, dt,
                        "UTWORZENIE",                       // pozostaje UTWORZENIE — Sergiusz widzi że to przy utworzeniu
                        "➕ UTWORZENIE pozycji",            // wizualnie odróżnione od głównego UTWORZENIA
                        handlowiec, odbiorca, uzytkownik, uzytkownikId,
                        nazwa, nazwa, kod, wszystkieTow,
                        opisSynth, dataUboju, searchAll,
                        KatKg, ";KG;UTWORZENIE;",           // ← główna KG + KategoriaSet multi-tag
                        pole, "0", wartoscNowa, zmianaDisp,
                        zmPrzed, zmArrow, zmBrush, zmPo,
                        (object?)synthImg ?? DBNull.Value,
                        synthDataZmiany, synthDataUboju,
                        ilosc >= AnomalyThresholdKg,
                        (object?)synthHandlAvatar ?? DBNull.Value);
                }
            }
        }

        // Format daty z dniem tygodnia po polsku — np. "Pn 09.05.2026" lub "Pt 09.05.2026 14:32".
        private static readonly string[] _dniSkrot = { "Nd", "Pn", "Wt", "Śr", "Cz", "Pt", "Sb" };
        private static string FormatPlDate(DateTime dt, bool withTime)
        {
            string dzien = _dniSkrot[(int)dt.DayOfWeek];
            return withTime
                ? $"{dzien} {dt:dd.MM.yyyy HH:mm}"
                : $"{dzien} {dt:dd.MM.yyyy}";
        }

        private static string TypIcon(string typ) => typ switch
        {
            "UTWORZENIE" => "➕",
            "EDYCJA" => "✏",
            "ANULOWANIE" => "❌",
            "PRZYWROCENIE" => "🔄",
            "USUNIECIE" => "🗑",
            _ => "•"
        };

        // Stałe kategorii — używane w combo filtrów i w kolorowaniu wierszy
        public const string KatUtworzenie = "UTWORZENIE";
        public const string KatAnulowanie = "ANULOWANIE";
        public const string KatPrzywrocenie = "PRZYWROCENIE";
        public const string KatUsuniecie = "USUNIECIE";
        public const string KatKg = "KG";
        public const string KatCena = "CENA";
        public const string KatTermin = "TERMIN";
        public const string KatTransport = "TRANSPORT";
        public const string KatNotatka = "NOTATKA";
        public const string KatFlagi = "FLAGI";
        public const string KatInne = "INNE";

        // Klasyfikuje wpis historii do jednej z kategorii — używane do quick-filtra.
        // Reguły: najpierw typZmiany (UTWORZENIE/ANULOWANIE itd.), potem heurystyka po PoleZmienione + opis + structured values.
        // PoleZmienione typowo: "Pozycja: {nazwa} - Zam." (kg), "Pozycja: {nazwa} - Cena", "Notatka", "DataPrzyjazdu" itd.
        private static string Categorize(string typZmiany, string? poleZmienione, string? opisZmiany,
            string? wartoscNowa = null, string? wartoscPoprzednia = null)
        {
            string typ = (typZmiany ?? "").ToUpperInvariant();
            if (typ == KatUtworzenie) return KatUtworzenie;
            if (typ == KatAnulowanie) return KatAnulowanie;
            if (typ == KatPrzywrocenie) return KatPrzywrocenie;
            if (typ == KatUsuniecie) return KatUsuniecie;

            string pole = (poleZmienione ?? "").ToLowerInvariant();
            string opis = (opisZmiany ?? "").ToLowerInvariant();
            string nowa = (wartoscNowa ?? "").ToLowerInvariant();
            string stara = (wartoscPoprzednia ?? "").ToLowerInvariant();

            // KG — handlowcy zmieniają ilość bardzo często (kluczowe dla Sergiusza).
            // Łapiemy: "Pozycja: X - Zam.", "Zmiana ilości", samo "kg" w opisie/wartosci, "dodano:", "usunięto:".
            bool kgMatch = pole.Contains("zam.") || pole.Contains(" zam ") || pole.EndsWith(" zam") || pole.EndsWith("- zam")
                        || pole.Contains("ilość") || pole.Contains("ilosc") || pole.EndsWith(" kg") || pole.Contains(" kg ")
                        || opis.Contains("zmiana ilości") || opis.Contains("zmiana ilosci")
                        || opis.StartsWith("dodano:") || opis.StartsWith("usunięto:") || opis.StartsWith("usunieto:")
                        || System.Text.RegularExpressions.Regex.IsMatch(opis, @"\d+\s*kg\b")
                        || System.Text.RegularExpressions.Regex.IsMatch(nowa, @"^\s*\d+(\.\d+)?\s*$")  // sama liczba w nowa
                            && System.Text.RegularExpressions.Regex.IsMatch(opis, @"\bkg\b");
            if (kgMatch) return KatKg;

            if (pole.Contains("cena") || opis.Contains("zmiana ceny")
                || System.Text.RegularExpressions.Regex.IsMatch(opis, @"\d+[,\.]\d+\s*zł\b")
                || System.Text.RegularExpressions.Regex.IsMatch(nowa, @"\d+[,\.]\d+\s*zł"))
                return KatCena;

            if (pole.Contains("transport") || pole.Contains("wlasn") || pole.Contains("własn")
                || pole.Contains("odbior") || pole.Contains("odbiór")
                || opis.Contains("transport")) return KatTransport;

            if (pole.Contains("data") || pole.Contains("godzin") || pole.Contains("termin")
                || pole == "dataprzyjazdu" || pole == "dataprodukcji" || pole == "datauboju")
                return KatTermin;

            if (pole.Contains("notatka") || pole.Contains("uwagi")) return KatNotatka;

            if (pole.Contains("e2") || pole.Contains("folia") || pole.Contains("halal")
                || pole.Contains("hallal") || pole.Contains("strefa"))
                return KatFlagi;

            return KatInne;
        }

        // Brushe dla kierunku zmiany — frozen, reuse między wierszami
        private static readonly System.Windows.Media.Brush ArrowUpBrush = MakeFrozenBrush("#16A34A");      // zielony (wzrost)
        private static readonly System.Windows.Media.Brush ArrowDownBrush = MakeFrozenBrush("#DC2626");    // czerwony (spadek)
        private static readonly System.Windows.Media.Brush ArrowSameBrush = MakeFrozenBrush("#64748B");    // szary (neutralny)
        private static readonly System.Windows.Media.Brush ArrowGrayBrush = MakeFrozenBrush("#94A3B8");    // szary jasny (placeholder)

        private static System.Windows.Media.Brush MakeFrozenBrush(string hex)
        {
            var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
            var b = new System.Windows.Media.SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        // Strukturalne rozkładanie zmiany na 4 elementy: przed, strzałka, brush strzałki, po + display.
        // Strzałka zielona ↑ dla wzrostów, czerwona ↓ dla spadków, szara → dla neutralnych/nieliczbowych.
        private static (string Przed, string Arrow, System.Windows.Media.Brush ArrowBrush, string Po, string Display)
            BuildZmiana(string kategoria, string? stara, string? nowa, string? opis)
        {
            string s = (stara ?? "").Trim();
            string n = (nowa ?? "").Trim();

            if (string.IsNullOrEmpty(s) && string.IsNullOrEmpty(n))
            {
                string special = kategoria switch
                {
                    KatUtworzenie => "(nowe zamówienie)",
                    KatAnulowanie => "(anulowane)",
                    KatPrzywrocenie => "(przywrócone)",
                    KatUsuniecie => "(usunięte)",
                    _ => ""
                };
                return ("", "", ArrowSameBrush, special, special);
            }

            // KG / CENA — parsuj jako liczby, kolorowa strzałka
            if (kategoria == KatKg || kategoria == KatCena)
            {
                if (TryParseNumber(s, out decimal sN) && TryParseNumber(n, out decimal nN))
                {
                    decimal diff = nN - sN;
                    string arrow = diff > 0 ? "↑" : diff < 0 ? "↓" : "→";
                    var brush = diff > 0 ? ArrowUpBrush : diff < 0 ? ArrowDownBrush : ArrowSameBrush;
                    string unit = kategoria == KatKg ? " kg" : " zł";
                    string sign = diff > 0 ? "+" : "";
                    // "—" zamiast 0 — oznacza "nie istniało" (utworzenie z niczego / usunięcie do niczego)
                    string przed = sN == 0 ? "—" : $"{sN:N0}{unit}";
                    string po = nN == 0 ? "—" : $"{nN:N0}{unit}";
                    string display = $"{przed}  {arrow}  {po}  ({sign}{diff:N0})";
                    return (przed, arrow, brush, po, display);
                }
            }

            // Domyślnie: tekstowy diff
            string sDisp = string.IsNullOrEmpty(s) ? "(puste)" : Truncate(s, 40);
            string nDisp = string.IsNullOrEmpty(n) ? "(puste)" : Truncate(n, 40);
            return (sDisp, "→", ArrowSameBrush, nDisp, $"{sDisp}  →  {nDisp}");
        }

        private static bool TryParseNumber(string s, out decimal result)
        {
            result = 0;
            if (string.IsNullOrEmpty(s)) return false;
            string clean = s.Replace(" ", "").Replace(",", ".").Trim();
            // Wyciągnij wiodącą liczbę (zignoruj jednostkę "kg", "zł" itp.)
            var m = System.Text.RegularExpressions.Regex.Match(clean, @"-?\d+(\.\d+)?");
            if (!m.Success) return false;
            return decimal.TryParse(m.Value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out result);
        }

        private static string Truncate(string s, int maxLen)
            => s.Length <= maxLen ? s : s.Substring(0, maxLen - 1) + "…";

        // Wyciąga nazwę towaru z PoleZmienione w formacie "Pozycja: NazwaTowaru - Atrybut"
        // (atrybut to "Zam." / "Cena" / "E2" / "Folia" / "Hallal" / "Strefa").
        // Zwraca "" jeśli format niepasujący.
        private static string ExtractTowarFromPoleZmienione(string? pole)
        {
            if (string.IsNullOrEmpty(pole)) return "";
            var match = System.Text.RegularExpressions.Regex.Match(pole, @"^Pozycja:\s*(.+?)\s*-\s*[^-]+$");
            return match.Success ? match.Groups[1].Value.Trim() : "";
        }

        // Heurystyka dla legacy wpisów bez structured fields (PoleZmienione/WartoscPoprzednia/WartoscNowa puste).
        // Parsuje OpisZmiany żeby wyciągnąć: nazwę towaru + atrybut + przed/po.
        // Łapie wzorce:
        //   "Dodano: Filet 3000 kg"                          → towar=Filet, atrybut=Zam., 0 → 3000
        //   "Usunięto: Filet (było 100 kg)"                  → towar=Filet, atrybut=Zam., 100 → 0
        //   "Filet: Zam. 100 → 150"                          → towar=Filet, atrybut=Zam., 100 → 150
        //   "(brak) > Filet A 3000 kg"  /  "Filet A 3000 kg" → towar=Filet A, atrybut=Zam., 0 → 3000
        //   "Filet: Cena 8,40 → 8,60"                        → towar=Filet, atrybut=Cena, 8,40 → 8,60
        private void EnrichLegacyEntry(HistoryRecord h)
        {
            // Już ma structured? Nic nie rób.
            bool hasStruct = !string.IsNullOrWhiteSpace(h.PoleZmienione)
                          || !string.IsNullOrWhiteSpace(h.WartoscPoprzednia)
                          || !string.IsNullOrWhiteSpace(h.WartoscNowa);
            if (hasStruct) return;

            string opis = (h.OpisZmiany ?? "").Trim();
            if (string.IsNullOrEmpty(opis)) return;

            // 1. "Dodano: NAZWA <liczba> kg"
            var mAdd = System.Text.RegularExpressions.Regex.Match(opis,
                @"Dodano:\s*(.+?)\s+([\d\s ]+)\s*kg",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mAdd.Success)
            {
                ApplyParsed(h, mAdd.Groups[1].Value, "Zam.", "0", CleanNumber(mAdd.Groups[2].Value));
                return;
            }

            // 2. "Usunięto: NAZWA (było <liczba> kg)"
            var mRem = System.Text.RegularExpressions.Regex.Match(opis,
                @"Usunięto:\s*(.+?)\s*\(było\s+([\d\s ]+)\s*kg",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mRem.Success)
            {
                ApplyParsed(h, mRem.Groups[1].Value, "Zam.", CleanNumber(mRem.Groups[2].Value), "0");
                return;
            }

            // 3. "NAZWA: Atrybut stara → nowa"
            var mChg = System.Text.RegularExpressions.Regex.Match(opis,
                @"^(.+?):\s*(Zam\.|Cena|E2|Folia|Hallal|Strefa|Notatka)\s+(.+?)\s*[→>]\s*(.+?)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mChg.Success)
            {
                ApplyParsed(h, mChg.Groups[1].Value, mChg.Groups[2].Value,
                            CleanNumber(mChg.Groups[3].Value), CleanNumber(mChg.Groups[4].Value));
                return;
            }

            // 4. "(brak/puste/—) > NAZWA <liczba> kg" — wpisy ze "starym = pusty"
            var mEmptyToKg = System.Text.RegularExpressions.Regex.Match(opis,
                @"(?:\(brak\)|\(puste\)|—|-)\s*[>→]\s*(.+?)\s+([\d\s ]+)\s*kg",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mEmptyToKg.Success)
            {
                ApplyParsed(h, mEmptyToKg.Groups[1].Value, "Zam.", "0", CleanNumber(mEmptyToKg.Groups[2].Value));
                return;
            }

            // 5. "NAZWA <liczba> kg" jako standalone (utworzenie / dodanie domyślne)
            var mPlain = System.Text.RegularExpressions.Regex.Match(opis,
                @"^(.+?)\s+([\d\s ]+)\s*kg\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (mPlain.Success)
            {
                ApplyParsed(h, mPlain.Groups[1].Value, "Zam.", "0", CleanNumber(mPlain.Groups[2].Value));
                return;
            }
        }

        private void ApplyParsed(HistoryRecord h, string towar, string atrybut, string stara, string nowa)
        {
            towar = (towar ?? "").Trim();
            if (string.IsNullOrEmpty(towar)) return;
            h.PoleZmienione = $"Pozycja: {towar} - {atrybut}";
            h.WartoscPoprzednia = stara;
            h.WartoscNowa = nowa;
            // Jeśli mamy mapping nazwa → KodTowaru i wpis go nie miał, wstrzyknij — pomoże filtrowaniu.
            if (h.KodTowaru == 0 && _productIdsByName.TryGetValue(towar, out int kod))
                h.KodTowaru = kod;
        }

        private static string CleanNumber(string s)
            => (s ?? "").Replace(" ", "").Replace(" ", "").Trim();

        // Wymuszony cleanup: jeśli "towar" zawiera szum z legacy zapisów ("(brak)", " > ", " kg", liczby),
        // wyciągnij realną nazwę przez fuzzy match z katalogu towarów (_productNames).
        private string CleanTowarName(string towar, int kodTowaru)
        {
            if (string.IsNullOrWhiteSpace(towar)) return towar ?? "";

            if (kodTowaru > 0 && _productNames.TryGetValue(kodTowaru, out var nameByKod))
                return nameByKod;

            if (_productIdsByName.ContainsKey(towar.Trim())) return towar.Trim();

            string brudny = towar;
            bool hasNoise = brudny.IndexOf("(brak)", StringComparison.OrdinalIgnoreCase) >= 0
                         || brudny.IndexOf("(puste)", StringComparison.OrdinalIgnoreCase) >= 0
                         || brudny.Contains(">") || brudny.Contains("→")
                         || System.Text.RegularExpressions.Regex.IsMatch(brudny, @"\d+\s*kg\b",
                                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!hasNoise) return brudny.Trim();

            // Fuzzy match — znajdź najdłuższą nazwę towaru z katalogu która występuje w "brudny"
            string best = "";
            foreach (var kv in _productNames)
            {
                string n = kv.Value;
                if (string.IsNullOrWhiteSpace(n)) continue;
                if (brudny.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0 && n.Length > best.Length)
                    best = n;
            }
            if (!string.IsNullOrEmpty(best)) return best;

            // Fallback: usuń znane szum-tokeny + liczby z jednostkami, potem fuzzy match raz jeszcze
            string clean = System.Text.RegularExpressions.Regex.Replace(brudny,
                @"\(brak\)|\(puste\)|→|>|\d+[\d\s]*\s*kg", "",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
            foreach (var kv in _productNames)
            {
                string n = kv.Value;
                if (string.IsNullOrWhiteSpace(n)) continue;
                if (clean.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                    return n;
            }
            return string.IsNullOrEmpty(clean) ? "" : clean;
        }

        private string ExtractProductFromDescription(string opis)
        {
            if (string.IsNullOrEmpty(opis)) return "";
            var patterns = new[]
            {
                @"Zmiana ilości:\s*(.+?)\s+z\s+\d",
                @"Zmiana ceny:\s*(.+?)\s+z\s+\d",
                @"Produkt:\s*(.+?)(?:\s*[-,]|$)",
                @"Towar:\s*(.+?)(?:\s*[-,]|$)",
                @"^(.+?)\s*[-:]\s*(?:ilość|cena|zmiana)"
            };
            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(opis, pattern,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    var product = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrEmpty(product) && product.Length > 2)
                        return product;
                }
            }
            return "";
        }

        // ════════════════════ FILTRY ════════════════════

        // Statyczna lista opcji w combo Kategoria — ustawiana raz w konstruktorze (nie zależy od danych)
        private void InitializeKategoriaCombo()
        {
            var opcje = new List<KategoriaOption>
            {
                new("(Wszystkie zmiany)", ""),
                new("📦 Ilość (kg)", KatKg),
                new("💰 Cena", KatCena),
                new("📅 Termin / data", KatTermin),
                new("🚚 Transport", KatTransport),
                new("📝 Notatka", KatNotatka),
                new("☑ Flagi (E2/Folia/Halal/Strefa)", KatFlagi),
                new("➕ Utworzenia", KatUtworzenie),
                new("❌ Anulowania", KatAnulowanie),
                new("🔄 Przywrócenia", KatPrzywrocenie),
                new("🗑 Usunięcia", KatUsuniecie),
                new("• Inne", KatInne)
            };
            cmbKategoria.ItemsSource = opcje;
            cmbKategoria.DisplayMemberPath = "Display";
            cmbKategoria.SelectedValuePath = "Value";
            cmbKategoria.SelectedIndex = 0;
        }

        public record KategoriaOption(string Display, string Value);

        public class TowarComboItem
        {
            public string Name { get; set; } = "";
            public ImageSource? Image { get; set; }
            public Visibility ImageVisibility => Image != null ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PopulateFilterComboBoxes()
        {
            // Combo Towar z miniaturami — pierwsza pozycja "(Wszystkie)" bez obrazka, reszta z TowaryZdjeciaService
            var towary = new List<TowarComboItem> { new() { Name = "(Wszystkie)" } };
            foreach (var kv in _productNames.OrderBy(x => x.Value))
            {
                towary.Add(new TowarComboItem
                {
                    Name = kv.Value,
                    Image = Kalendarz1.AnalitykaPelna.Services.TowaryZdjeciaService.Get(kv.Key)
                });
            }
            cmbTowar.ItemsSource = towary;
            cmbTowar.SelectedIndex = 0;

            var users = new List<string> { "(Wszystkie)" };
            var odbiorcy = new List<string> { "(Wszystkie)" };
            var typy = new List<string> { "(Wszystkie)" };
            var handlowcy = new List<string> { "(Wszystkie)" };

            foreach (DataRow row in _dtHistoria.Rows)
            {
                var user = row["UzytkownikNazwa"]?.ToString();
                var odbiorca = row["Odbiorca"]?.ToString();
                var typ = row["TypZmiany"]?.ToString();
                var handlowiec = row["Handlowiec"]?.ToString();

                if (!string.IsNullOrEmpty(user) && !users.Contains(user)) users.Add(user);
                if (!string.IsNullOrEmpty(odbiorca) && !odbiorcy.Contains(odbiorca)) odbiorcy.Add(odbiorca);
                if (!string.IsNullOrEmpty(typ) && !typy.Contains(typ)) typy.Add(typ);
                if (!string.IsNullOrEmpty(handlowiec) && !handlowcy.Contains(handlowiec)) handlowcy.Add(handlowiec);
            }

            cmbKtoEdytowal.ItemsSource = users.OrderBy(x => x).ToList();
            cmbOdbiorca.ItemsSource = odbiorcy.OrderBy(x => x).ToList();
            cmbTyp.ItemsSource = typy.OrderBy(x => x).ToList();
            cmbHandlowiec.ItemsSource = handlowcy.OrderBy(x => x).ToList();

            cmbKtoEdytowal.SelectedIndex = 0;
            cmbOdbiorca.SelectedIndex = 0;
            cmbTyp.SelectedIndex = 0;
            cmbHandlowiec.SelectedIndex = 0;
        }

        private void ApplyFilters()
        {
            if (_suppressFilters) return;
            var dv = _dtHistoria.DefaultView;
            var filters = new List<string>();

            // Filtr kategorii — LIKE na KategoriaSet (multi-tag) żeby utworzenia pozycji
            // (oznaczone ";KG;UTWORZENIE;") łapały się pod oba filtry.
            string kategoriaValue = cmbKategoria.SelectedValue as string ?? "";
            if (!string.IsNullOrEmpty(kategoriaValue))
            {
                filters.Add($"KategoriaSet LIKE '*;{kategoriaValue};*'");
            }

            // Filtr towaru — kluczowa zmiana: matchuj po WszystkieTowary (pozycje zamówienia)
            // → UTWORZENIE/EDYCJA zamówienia z danym towarem teraz się pokaże, nawet gdy KodTowaru w wpisie = 0.
            string wybranyTowarText = (cmbTowar.SelectedItem as TowarComboItem)?.Name ?? "(Wszystkie)";
            if (wybranyTowarText != "(Wszystkie)")
            {
                if (_productIdsByName.TryGetValue(wybranyTowarText, out int kodFiltr))
                {
                    string escNazwa = EscapeLikePattern(wybranyTowarText.Replace("'", "''"));
                    // Precyzyjny match — TYLKO wpisy dotyczące tego konkretnego towaru:
                    //   1. KodTowaru = filtr — wpis historii bezpośrednio o tym towarze
                    //   2. PoleZmienione LIKE "Pozycja: Filet -" — structured log per pozycja
                    // (Pomijamy WszystkieTowary i OpisZmiany żeby nie łapać zmian INNYCH towarów w tym samym zamówieniu.)
                    filters.Add($"(KodTowaru = {kodFiltr} OR PoleZmienione LIKE '*Pozycja: {escNazwa} -*')");
                    txtHistoriaTitle.Text = $"HISTORIA ZMIAN — {wybranyTowarText.ToUpper()}";
                }
                else
                {
                    txtHistoriaTitle.Text = $"HISTORIA ZMIAN — {wybranyTowarText.ToUpper()}";
                }
            }
            else
            {
                txtHistoriaTitle.Text = "HISTORIA ZMIAN";
            }

            if (cmbKtoEdytowal.SelectedItem?.ToString() is string user && user != "(Wszystkie)")
                filters.Add($"UzytkownikNazwa = '{user.Replace("'", "''")}'");

            if (cmbOdbiorca.SelectedItem?.ToString() is string odbiorca && odbiorca != "(Wszystkie)")
                filters.Add($"Odbiorca = '{odbiorca.Replace("'", "''")}'");

            if (cmbTyp.SelectedItem?.ToString() is string typ && typ != "(Wszystkie)")
                filters.Add($"TypZmiany = '{typ.Replace("'", "''")}'");

            if (cmbHandlowiec.SelectedItem?.ToString() is string handlowiec && handlowiec != "(Wszystkie)")
                filters.Add($"Handlowiec = '{handlowiec.Replace("'", "''")}'");

            // Search tekstowy — LIKE w SearchAll (skonkatenowane pola)
            string searchText = (txtSearch?.Text ?? "").Trim();
            if (!string.IsNullOrEmpty(searchText))
            {
                string escSearch = EscapeLikePattern(searchText.Replace("'", "''"));
                filters.Add($"SearchAll LIKE '*{escSearch}*'");
            }

            dv.RowFilter = filters.Count > 0 ? string.Join(" AND ", filters) : "";

            // Dynamicznie pokaż w kolumnie Towar nazwę + ilość gdy filtr towaru aktywny
            UpdateTowarPokazany();
            UpdateDashboardCounters();
            UpdateItemsSource();   // przebuduj group view jeśli aktywne grupowanie + empty state
            UpdateActiveFilterChips();
            UpdateMiniChart();
            UpdateTopAnomalie();
            UpdateHeatmap();
        }

        // Aktualizuje kolumnę TowarPokazany w DataTable bazując na aktualnym filtrze towaru.
        // Gdy filtr aktywny i zamówienie ma ten towar w pozycjach → "Filet 100 kg".
        // Gdy filtr wyczyszczony → wracamy do oryginalnego Towar (z wpisu historii).
        private void UpdateTowarPokazany()
        {
            int filtrKod = 0;
            string filtrNazwa = "";
            if (cmbTowar.SelectedItem is TowarComboItem ti && ti.Name != "(Wszystkie)")
            {
                if (_productIdsByName.TryGetValue(ti.Name, out filtrKod))
                    filtrNazwa = ti.Name;
            }

            foreach (DataRow row in _dtHistoria.Rows)
            {
                if (filtrKod == 0)
                {
                    // Brak filtra towaru — pokaż oryginalny Towar
                    row["TowarPokazany"] = row["Towar"];
                    continue;
                }

                // Sergiusz: w kolumnie Towar tylko nazwa (bez "Kurczak A 4 5000 kg" — ilość jest w kolumnie Zmiana).
                if (Convert.ToInt32(row["KodTowaru"]) == filtrKod)
                    row["TowarPokazany"] = filtrNazwa;
                else if (_orderItemsByOrder.TryGetValue(Convert.ToInt32(row["ZamowienieId"]), out var pozycje) && pozycje.ContainsKey(filtrKod))
                    row["TowarPokazany"] = filtrNazwa;
                else
                    row["TowarPokazany"] = row["Towar"];
            }
        }

        // Escape dla DataView.RowFilter LIKE: '[', ']', '*', '%' są wildcardami
        private static string EscapeLikePattern(string s)
        {
            if (string.IsNullOrEmpty(s)) return s ?? "";
            return s.Replace("[", "[[]").Replace("*", "[*]").Replace("%", "[%]");
        }

        private void UpdateDashboardCounters()
        {
            var dv = _dtHistoria.DefaultView;
            int total = dv.Count;
            int utw = 0, edy = 0, anu = 0, prz = 0, usu = 0;
            for (int i = 0; i < dv.Count; i++)
            {
                string typ = dv[i].Row["TypZmiany"]?.ToString() ?? "";
                switch (typ)
                {
                    case "UTWORZENIE": utw++; break;
                    case "EDYCJA": edy++; break;
                    case "ANULOWANIE": anu++; break;
                    case "PRZYWROCENIE": prz++; break;
                    case "USUNIECIE": usu++; break;
                }
            }
            txtTotalChanges.Text = total.ToString("N0");
            txtStatUtw.Text = utw.ToString("N0");
            txtStatEdy.Text = edy.ToString("N0");
            txtStatAnu.Text = (anu + prz).ToString("N0"); // ANU pokazuje też przywrócenia (zmiany statusu)
            txtStatUsu.Text = usu.ToString("N0");
            txtDisplayedCount.Text = total.ToString("N0");
        }

        // ════════════════════ FILTER CHIPS (klikalne X) ════════════════════

        public class FilterChip
        {
            public string Id { get; set; } = "";
            public string Label { get; set; } = "";
            public Brush Color { get; set; } = Brushes.Gray;
        }

        private void UpdateActiveFilterChips()
        {
            var chips = new List<FilterChip>();
            string val = cmbKategoria.SelectedValue as string ?? "";
            if (!string.IsNullOrEmpty(val))
                chips.Add(new FilterChip { Id = "kat", Label = "📊 " + (cmbKategoria.SelectedItem as KategoriaOption)?.Display, Color = MakeFrozenBrush("#0EA5E9") });

            if (cmbTowar.SelectedItem is TowarComboItem tw && tw.Name != "(Wszystkie)")
                chips.Add(new FilterChip { Id = "towar", Label = "🍗 " + tw.Name, Color = MakeFrozenBrush("#C0392B") });

            if (cmbTyp.SelectedItem is string typ && typ != "(Wszystkie)")
                chips.Add(new FilterChip { Id = "typ", Label = "Typ: " + typ, Color = MakeFrozenBrush("#475569") });

            if (cmbOdbiorca.SelectedItem is string od && od != "(Wszystkie)")
                chips.Add(new FilterChip { Id = "odbiorca", Label = "👥 " + od, Color = MakeFrozenBrush("#7C3AED") });

            if (cmbHandlowiec.SelectedItem is string h && h != "(Wszystkie)")
                chips.Add(new FilterChip { Id = "handlowiec", Label = "🧑‍💼 " + h, Color = MakeFrozenBrush("#0369A1") });

            if (cmbKtoEdytowal.SelectedItem is string u && u != "(Wszystkie)")
                chips.Add(new FilterChip { Id = "ktoedytowal", Label = "✏ " + u, Color = MakeFrozenBrush("#16A34A") });

            if (!string.IsNullOrEmpty(txtSearch.Text?.Trim()))
                chips.Add(new FilterChip { Id = "search", Label = "🔍 " + txtSearch.Text.Trim(), Color = MakeFrozenBrush("#64748B") });

            lstActiveFilters.ItemsSource = chips;
            ActiveFiltersPanel.Visibility = chips.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnRemoveFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string id) return;
            _suppressFilters = true;
            switch (id)
            {
                case "kat": cmbKategoria.SelectedIndex = 0; break;
                case "towar": cmbTowar.SelectedIndex = 0; break;
                case "typ": cmbTyp.SelectedIndex = 0; break;
                case "odbiorca": cmbOdbiorca.SelectedIndex = 0; break;
                case "handlowiec": cmbHandlowiec.SelectedIndex = 0; break;
                case "ktoedytowal": cmbKtoEdytowal.SelectedIndex = 0; break;
                case "search": txtSearch.Text = ""; break;
            }
            _suppressFilters = false;
            ApplyFilters();
        }

        // ════════════════════ QUICK PRESETS ════════════════════

        private void BtnQuickPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not string tag) return;
            _suppressFilters = true;
            switch (tag)
            {
                case "dzis":
                    dpDzienUboju.SelectedDate = DateTime.Today;
                    cmbKategoria.SelectedIndex = 0;
                    cmbTowar.SelectedIndex = 0;
                    cmbTyp.SelectedIndex = 0;
                    cmbOdbiorca.SelectedIndex = 0;
                    cmbHandlowiec.SelectedIndex = 0;
                    cmbKtoEdytowal.SelectedIndex = 0;
                    txtSearch.Text = "";
                    _suppressFilters = false;
                    _ = LoadDataAsync();
                    return;
                case "ja":
                    string fullName = App.UserFullName ?? "";
                    if (cmbKtoEdytowal.Items.Count > 0)
                    {
                        for (int i = 0; i < cmbKtoEdytowal.Items.Count; i++)
                        {
                            if (string.Equals(cmbKtoEdytowal.Items[i]?.ToString(), fullName, StringComparison.OrdinalIgnoreCase))
                            { cmbKtoEdytowal.SelectedIndex = i; break; }
                        }
                    }
                    break;
                case "kg":
                    SelectKategoria(KatKg);
                    break;
                case "anomalie":
                    // ustawi się przez RowFilter w ApplyFilters poniżej
                    SelectKategoria(KatKg);
                    break;
                case "obserwowane":
                    // filter w RowFilter
                    break;
            }
            _suppressFilters = false;

            // dla "anomalie" i "obserwowane" — dodatkowy filtr wstrzykiwany do RowFilter
            if (tag == "anomalie")
            {
                ApplyFilters();
                var dv = _dtHistoria.DefaultView;
                dv.RowFilter = string.IsNullOrEmpty(dv.RowFilter) ? "IsAnomalia = 1" : dv.RowFilter + " AND IsAnomalia = 1";
                UpdateTowarPokazany();
                UpdateDashboardCounters();
                UpdateItemsSource();
                UpdateMiniChart();
                UpdateTopAnomalie();
                return;
            }
            if (tag == "obserwowane")
            {
                if (_bookmarks.Count == 0)
                {
                    MessageBox.Show("Nie masz jeszcze obserwowanych zamówień.\nKliknij ☆ przy zamówieniu w panelu szczegółów żeby dodać.",
                        "Brak obserwowanych", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                ApplyFilters();
                var dv = _dtHistoria.DefaultView;
                string ids = string.Join(",", _bookmarks);
                string extra = $"ZamowienieId IN ({ids})";
                dv.RowFilter = string.IsNullOrEmpty(dv.RowFilter) ? extra : dv.RowFilter + " AND " + extra;
                UpdateTowarPokazany();
                UpdateDashboardCounters();
                UpdateItemsSource();
                UpdateMiniChart();
                UpdateTopAnomalie();
                return;
            }
            ApplyFilters();
        }

        private void SelectKategoria(string value)
        {
            for (int i = 0; i < cmbKategoria.Items.Count; i++)
            {
                if (cmbKategoria.Items[i] is KategoriaOption ko && ko.Value == value)
                { cmbKategoria.SelectedIndex = i; return; }
            }
        }

        // ════════════════════ AUTO-REFRESH ════════════════════

        private void BtnAutoRefresh_Click(object sender, RoutedEventArgs e)
        {
            bool on = btnAutoRefresh.IsChecked == true;
            if (autoRefreshText != null)
            {
                autoRefreshText.Text = on ? "🔄 Auto-odświeżanie 60s" : "🔄 Auto-odświeżanie OFF";
                autoRefreshText.Foreground = on ? Brushes.White : (Brush)FindResource("TextPrimary");
            }
            if (on)
            {
                _autoRefreshTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
                _autoRefreshTimer.Tick += (_, _) => _ = LoadDataAsync();
                _autoRefreshTimer.Start();
            }
            else
            {
                _autoRefreshTimer?.Stop();
                _autoRefreshTimer = null;
            }
        }

        // ════════════════════ MINI CHART godzin (rozkład zmian) ════════════════════

        public class HourBar
        {
            public int Hour { get; set; }
            public int Count { get; set; }
            public double BarHeight { get; set; }
            public Brush Color { get; set; } = Brushes.Gray;
            public string Tooltip { get; set; } = "";
        }

        private void UpdateMiniChart()
        {
            // Liczymy zmiany per godzina 0-23 z widocznego DataView
            var hours = new int[24];
            var dv = _dtHistoria.DefaultView;
            for (int i = 0; i < dv.Count; i++)
            {
                var dt = (DateTime)dv[i].Row["DataZmiany"];
                if (dt.Hour >= 0 && dt.Hour < 24) hours[dt.Hour]++;
            }
            int max = hours.Max();
            if (max == 0) max = 1;
            var bars = new List<HourBar>();
            var brushBar = MakeFrozenBrush("#6BA044");
            for (int h = 0; h < 24; h++)
            {
                bars.Add(new HourBar
                {
                    Hour = h,
                    Count = hours[h],
                    BarHeight = (hours[h] / (double)max) * 70.0 + (hours[h] > 0 ? 4 : 0),
                    Color = brushBar,
                    Tooltip = $"{h:00}:00 — {hours[h]} zmian"
                });
            }
            lstChartHours.ItemsSource = bars;
        }

        // ════════════════════ HEATMAP dni × godziny ════════════════════

        public class HeatmapCell
        {
            public int Day { get; set; }
            public int Hour { get; set; }
            public int Count { get; set; }
            public Brush ColorBrush { get; set; } = Brushes.White;
            public string Tooltip { get; set; } = "";
        }

        private void UpdateHeatmap()
        {
            // 7 dni × 24 godziny — kolor = intensywność per komórka
            var grid = new int[7, 24];
            var dv = _dtHistoria.DefaultView;
            for (int i = 0; i < dv.Count; i++)
            {
                var dt = (DateTime)dv[i].Row["DataZmiany"];
                int dow = (int)dt.DayOfWeek;          // Sun=0..Sat=6
                int day = dow == 0 ? 6 : dow - 1;     // Mon=0..Sun=6 (PL convention)
                int hour = dt.Hour;
                if (hour < 0 || hour > 23) continue;
                grid[day, hour]++;
            }

            int max = 0;
            for (int d = 0; d < 7; d++)
                for (int h = 0; h < 24; h++)
                    if (grid[d, h] > max) max = grid[d, h];
            if (max == 0) max = 1;

            var cells = new List<HeatmapCell>();
            string[] dni = { "Pn", "Wt", "Śr", "Cz", "Pt", "Sb", "Nd" };
            for (int d = 0; d < 7; d++)
            {
                for (int h = 0; h < 24; h++)
                {
                    int c = grid[d, h];
                    double intensity = c / (double)max;
                    // Gradient: biały (#F1F5F9) → ciemnozielony (#46682C)
                    byte r = (byte)(0xF1 - (0xF1 - 0x46) * intensity);
                    byte g = (byte)(0xF5 - (0xF5 - 0x68) * intensity);
                    byte b = (byte)(0xF9 - (0xF9 - 0x2C) * intensity);
                    var brush = c == 0
                        ? MakeFrozenBrush("#E2E8F0")     // jasny szary widoczny zamiast prawie-białego
                        : new SolidColorBrush(Color.FromRgb(r, g, b));
                    if (c > 0) brush.Freeze();
                    cells.Add(new HeatmapCell
                    {
                        Day = d,
                        Hour = h,
                        Count = c,
                        ColorBrush = brush,
                        Tooltip = c == 0 ? $"{dni[d]} {h:00}:00 — brak zmian" : $"{dni[d]} {h:00}:00 — {c} zmian"
                    });
                }
            }
            lstHeatmap.ItemsSource = cells;
        }

        // ════════════════════ TOP ANOMALIE (side panel) ════════════════════

        public class AnomaliaItem
        {
            public string Towar { get; set; } = "";
            public string Display { get; set; } = "";
        }

        private void UpdateTopAnomalie()
        {
            var dv = _dtHistoria.DefaultView;
            var list = new List<AnomaliaItem>();
            for (int i = 0; i < dv.Count; i++)
            {
                var row = dv[i].Row;
                if (!(bool)row["IsAnomalia"]) continue;
                string towar = row["TowarPokazany"]?.ToString() ?? "(?)";
                string zmiana = row["ZmianaDisplay"]?.ToString() ?? "";
                list.Add(new AnomaliaItem { Towar = Truncate(towar, 18), Display = Truncate(zmiana, 24) });
                if (list.Count >= 5) break;
            }
            lstSideAnomalie.ItemsSource = list;
        }

        // ════════════════════ SIDE PANEL — szczegóły wybranego wpisu ════════════════════

        public class DetailRow
        {
            public string Label { get; set; } = "";
            public string Value { get; set; } = "";
        }

        public class TagViewVm
        {
            public int Id { get; set; }
            public string Display { get; set; } = "";
            public Brush BgColor { get; set; } = Brushes.Gray;
        }

        private void DgHistoria_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DataRow? selected = null;
            if (dgHistoria.SelectedItem is DataRowView drv) selected = drv.Row;
            _selectedRow = selected;
            UpdateSidePanel();
        }

        private void UpdateSidePanel()
        {
            if (_selectedRow == null)
            {
                SideDetailsEmpty.Visibility = Visibility.Visible;
                SideDetailsContent.Visibility = Visibility.Collapsed;
                return;
            }

            SideDetailsEmpty.Visibility = Visibility.Collapsed;
            SideDetailsContent.Visibility = Visibility.Visible;

            int zamId = Convert.ToInt32(_selectedRow["ZamowienieId"]);
            string odbiorca = _selectedRow["Odbiorca"]?.ToString() ?? "";
            string handlowiec = _selectedRow["Handlowiec"]?.ToString() ?? "";
            string user = _selectedRow["UzytkownikNazwa"]?.ToString() ?? "";
            string typ = _selectedRow["TypZmianyDisplay"]?.ToString() ?? "";
            string towar = _selectedRow["TowarPokazany"]?.ToString() ?? "";
            string opis = _selectedRow["OpisZmiany"]?.ToString() ?? "";
            string zmiana = _selectedRow["ZmianaDisplay"]?.ToString() ?? "";

            SideOrderTitle.Text = $"Zamówienie #{zamId}";
            SideOrderSub.Text = !string.IsNullOrEmpty(odbiorca) ? odbiorca : "(brak klienta)";

            var details = new List<DetailRow>
            {
                new() { Label = "Data zmiany", Value = _selectedRow["DataZmianyDisplay"]?.ToString() ?? "" },
                new() { Label = "Data uboju", Value = _selectedRow["DataUbojuDisplay"]?.ToString() ?? "" },
                new() { Label = "Typ", Value = typ },
                new() { Label = "Handlowiec", Value = string.IsNullOrEmpty(handlowiec) ? "—" : handlowiec },
                new() { Label = "Użytkownik", Value = user },
                new() { Label = "Towar", Value = towar },
                new() { Label = "Zmiana", Value = zmiana },
                new() { Label = "Opis", Value = opis }
            };
            lstSideDetails.ItemsSource = details;

            btnBookmark.IsChecked = _bookmarks.Contains(zamId);
            UpdateSideTags();
        }

        private void UpdateSideTags()
        {
            if (_selectedRow == null) { lstSideTags.ItemsSource = null; return; }
            int hid = Convert.ToInt32(_selectedRow["Id"]);
            if (hid < 0 || !_tagsByHistoriaId.TryGetValue(hid, out var tags))
            {
                lstSideTags.ItemsSource = null;
                return;
            }
            var vm = tags.Select(t => new TagViewVm
            {
                Id = t.Id,
                Display = t.Tag switch
                {
                    "OK" => "✅ OK · " + t.UserId,
                    "DoWyjasnienia" => "⚠ Do wyjaśnienia · " + t.UserId,
                    _ => t.Tag + " · " + t.UserId
                },
                BgColor = t.Tag switch
                {
                    "OK" => MakeFrozenBrush("#16A34A"),
                    "DoWyjasnienia" => MakeFrozenBrush("#F59E0B"),
                    _ => MakeFrozenBrush("#64748B")
                }
            }).ToList();
            lstSideTags.ItemsSource = vm;
        }

        private async void BtnBookmark_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRow == null) return;
            int zamId = Convert.ToInt32(_selectedRow["ZamowienieId"]);
            bool add = btnBookmark.IsChecked == true;
            try
            {
                await Kalendarz1.Services.HistoriaZmianMetaService.ToggleBookmarkAsync(zamId, _userId, add);
                if (add) _bookmarks.Add(zamId);
                else _bookmarks.Remove(zamId);
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Bookmark] {ex.Message}"); }
        }

        private async void BtnTag_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRow == null) return;
            if (sender is not Button b || b.Tag is not string tagName) return;
            int hid = Convert.ToInt32(_selectedRow["Id"]);
            if (hid < 0) return;   // syntetyczne wpisy (ujemne id) — nie tagujemy
            try
            {
                int newId = await Kalendarz1.Services.HistoriaZmianMetaService.AddTagAsync(hid, tagName, "", _userId);
                if (!_tagsByHistoriaId.TryGetValue(hid, out var list))
                {
                    list = new List<Kalendarz1.Services.HistoriaZmianMetaService.TagInfo>();
                    _tagsByHistoriaId[hid] = list;
                }
                list.Add(new Kalendarz1.Services.HistoriaZmianMetaService.TagInfo
                {
                    Id = newId, Tag = tagName, UserId = _userId, DataDodania = DateTime.Now
                });
                UpdateSideTags();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Tag] {ex.Message}"); }
        }

        private async void BtnRemoveTag_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button b || b.Tag is not int tagId) return;
            try
            {
                await Kalendarz1.Services.HistoriaZmianMetaService.RemoveTagAsync(tagId);
                foreach (var kv in _tagsByHistoriaId)
                    kv.Value.RemoveAll(t => t.Id == tagId);
                UpdateSideTags();
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[RemoveTag] {ex.Message}"); }
        }

        // ════════════════════ EVENT HANDLERS ════════════════════

        private void Filter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _suppressFilters) return;
            ApplyFilters();
        }

        private void DzienUboju_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded || _suppressFilters) return;
            _ = LoadDataAsync();   // reload z SQL bo filtr DataUboju idzie do query
        }

        private void BtnClearDay_Click(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            _suppressFilters = true;
            dpDzienUboju.SelectedDate = null;
            _suppressFilters = false;
            _ = LoadDataAsync();
        }

        private void BtnGrouping_Click(object sender, RoutedEventArgs e)
        {
            _isGrouped = btnGrouping?.IsChecked == true;
            if (textGrouping != null)
                textGrouping.Text = _isGrouped ? "Grupowanie" : "Grupuj";
            if (_isGrouped && textGrouping != null)
                textGrouping.Foreground = System.Windows.Media.Brushes.White;
            else if (textGrouping != null)
                textGrouping.Foreground = (System.Windows.Media.Brush)FindResource("TextPrimary");
            UpdateItemsSource();
        }

        private void BtnAnaliza_Click(object sender, RoutedEventArgs e)
        {
            if (PopupAnaliza == null) return;
            PopupAnaliza.IsOpen = !PopupAnaliza.IsOpen;
            if (PopupAnaliza.IsOpen) ComputeAndShowAnalytics();
        }

        // ════════════════════ ANALIZA ════════════════════
        // Liczone z aktualnie widocznych wierszy (DefaultView z filtrami).
        // Top: użytkownicy, zamówienia, towary. Suma delta kg (wzrost/spadek/netto).

        private void ComputeAndShowAnalytics()
        {
            try
            {
                var dv = _dtHistoria.DefaultView;
                int total = dv.Count;
                if (txtAnalizaScope != null) txtAnalizaScope.Text = $"{total:N0} wierszy";

                decimal sumaWzrost = 0m, sumaSpadek = 0m;
                var userCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var orderCount = new Dictionary<int, (int cnt, string klient)>();
                var towarCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < dv.Count; i++)
                {
                    var row = dv[i].Row;
                    string typ = row["TypZmiany"]?.ToString() ?? "";
                    string kategoria = row["Kategoria"]?.ToString() ?? "";
                    string user = row["UzytkownikNazwa"]?.ToString() ?? "(nieznany)";
                    int zamId = Convert.ToInt32(row["ZamowienieId"]);
                    string odbiorca = row["Odbiorca"]?.ToString() ?? "";
                    string towar = row["Towar"]?.ToString() ?? "";

                    // Liczniki
                    if (!string.IsNullOrWhiteSpace(user))
                    {
                        userCount.TryGetValue(user, out int u);
                        userCount[user] = u + 1;
                    }
                    if (orderCount.TryGetValue(zamId, out var oc)) orderCount[zamId] = (oc.cnt + 1, oc.klient);
                    else orderCount[zamId] = (1, odbiorca);

                    if (!string.IsNullOrWhiteSpace(towar) && kategoria == KatKg)
                    {
                        towarCount.TryGetValue(towar, out int t);
                        towarCount[towar] = t + 1;
                    }

                    // Delta kg — tylko dla kategorii KG ze structured values
                    if (kategoria == KatKg)
                    {
                        string s = row["WartoscPoprzednia"]?.ToString() ?? "";
                        string n = row["WartoscNowa"]?.ToString() ?? "";
                        if (decimal.TryParse(s.Replace(",", ".").Replace(" ", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal sN)
                            && decimal.TryParse(n.Replace(",", ".").Replace(" ", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal nN))
                        {
                            decimal diff = nN - sN;
                            if (diff > 0) sumaWzrost += diff;
                            else if (diff < 0) sumaSpadek += -diff;   // spadek jako wartość dodatnia
                        }
                    }
                }

                // Delta kg display
                txtAnalizaWzrost.Text = $"+{sumaWzrost:N0} kg";
                txtAnalizaSpadek.Text = $"-{sumaSpadek:N0} kg";
                decimal netto = sumaWzrost - sumaSpadek;
                string nettoSign = netto >= 0 ? "+" : "";
                txtAnalizaNetto.Text = $"Netto: {nettoSign}{netto:N0} kg";

                // Top 5 listy
                lstTopUserzy.ItemsSource = userCount
                    .OrderByDescending(kv => kv.Value)
                    .Take(5)
                    .Select(kv => new AnalitykaItem { Name = kv.Key, Count = kv.Value.ToString("N0") })
                    .ToList();

                lstTopZamowienia.ItemsSource = orderCount
                    .OrderByDescending(kv => kv.Value.cnt)
                    .Take(5)
                    .Select(kv => new AnalitykaItem
                    {
                        Name = string.IsNullOrEmpty(kv.Value.klient) ? $"#{kv.Key}" : $"#{kv.Key} · {Truncate(kv.Value.klient, 22)}",
                        Count = kv.Value.cnt.ToString("N0")
                    })
                    .ToList();

                lstTopTowary.ItemsSource = towarCount
                    .OrderByDescending(kv => kv.Value)
                    .Take(5)
                    .Select(kv => new AnalitykaItem { Name = Truncate(kv.Key, 26), Count = kv.Value.ToString("N0") })
                    .ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Analityka] {ex.Message}");
            }
        }

        public class AnalitykaItem
        {
            public string Name { get; set; } = "";
            public string Count { get; set; } = "";
        }

        // Custom IComparer dla DataRowView w ListCollectionView — sort po wielu kolumnach.
        // Tuple (column, ascending) — kolumna w DataRowView, true=ASC, false=DESC.
        public class DataRowViewMultiSort : System.Collections.IComparer
        {
            private readonly (string Col, bool Asc)[] _keys;
            public DataRowViewMultiSort(params (string Col, bool Asc)[] keys) { _keys = keys; }

            public int Compare(object? x, object? y)
            {
                if (x is not DataRowView rx || y is not DataRowView ry) return 0;
                foreach (var (col, asc) in _keys)
                {
                    object vx = rx[col], vy = ry[col];
                    int cmp;
                    if (vx is IComparable cx && vy is IComparable cy)
                        cmp = cx.CompareTo(cy);
                    else
                        cmp = string.Compare(vx?.ToString(), vy?.ToString(), StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return asc ? cmp : -cmp;
                }
                return 0;
            }
        }

        // Custom GroupDescription dla DataRowView — dostęp do kolumn przez indekser, nie przez reflection.
        // Standardowy PropertyGroupDescription nie znajdzie kolumn DataTable.
        public class DataRowViewGroupDescription : GroupDescription
        {
            private readonly string _column;
            public DataRowViewGroupDescription(string column) { _column = column; }
            public override object? GroupNameFromItem(object? item, int level, System.Globalization.CultureInfo culture)
            {
                if (item is DataRowView drv) return drv[_column];
                return null;
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded || _suppressFilters) return;
            ApplyFilters();
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            _suppressFilters = true;
            cmbKategoria.SelectedIndex = 0;
            cmbTowar.SelectedIndex = 0;
            cmbKtoEdytowal.SelectedIndex = 0;
            cmbOdbiorca.SelectedIndex = 0;
            cmbTyp.SelectedIndex = 0;
            cmbHandlowiec.SelectedIndex = 0;
            dpDzienUboju.SelectedDate = DateTime.Today;   // wracaj do dzisiaj (nie null)
            txtSearch.Text = "";
            txtHistoriaTitle.Text = "HISTORIA ZMIAN";
            _suppressFilters = false;
            _ = LoadDataAsync();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => _ = LoadDataAsync();
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ════════════════════ EKSPORT EXCEL ════════════════════

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dv = _dtHistoria.DefaultView;
                if (dv.Count == 0)
                {
                    MessageBox.Show("Brak danych do eksportu.", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var dlg = new SaveFileDialog
                {
                    Filter = "Plik Excel (*.xlsx)|*.xlsx",
                    FileName = $"HistoriaZmian_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };
                if (dlg.ShowDialog() != true) return;

                Mouse.OverrideCursor = Cursors.Wait;
                try
                {
                    using var wb = new XLWorkbook();
                    var ws = wb.Worksheets.Add("Historia zmian");

                    string[] headers = { "Data zmiany", "Typ", "Kategoria", "Odbiorca", "Handlowiec", "Użytkownik", "Towar", "Zmiana (przed → po)", "Opis zmiany", "Data uboju", "Zamówienie #" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = ws.Cell(1, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromArgb(70, 104, 44);
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }

                    int r = 2;
                    for (int i = 0; i < dv.Count; i++)
                    {
                        var row = dv[i].Row;
                        ws.Cell(r, 1).Value = (DateTime)row["DataZmiany"];
                        ws.Cell(r, 1).Style.NumberFormat.Format = "yyyy-mm-dd hh:mm";
                        ws.Cell(r, 2).Value = row["TypZmianyDisplay"]?.ToString() ?? "";
                        ws.Cell(r, 3).Value = row["Kategoria"]?.ToString() ?? "";
                        ws.Cell(r, 4).Value = row["Odbiorca"]?.ToString() ?? "";
                        ws.Cell(r, 5).Value = row["Handlowiec"]?.ToString() ?? "";
                        ws.Cell(r, 6).Value = row["UzytkownikNazwa"]?.ToString() ?? "";
                        ws.Cell(r, 7).Value = row["TowarPokazany"]?.ToString() ?? "";
                        ws.Cell(r, 8).Value = row["ZmianaDisplay"]?.ToString() ?? "";
                        ws.Cell(r, 9).Value = row["OpisZmiany"]?.ToString() ?? "";
                        if (row["DataUboju"] != DBNull.Value)
                        {
                            ws.Cell(r, 10).Value = (DateTime)row["DataUboju"];
                            ws.Cell(r, 10).Style.NumberFormat.Format = "yyyy-mm-dd";
                        }
                        ws.Cell(r, 11).Value = Convert.ToInt32(row["ZamowienieId"]);
                        r++;
                    }

                    ws.RangeUsed().SetAutoFilter();
                    ws.Columns().AdjustToContents();
                    ws.SheetView.FreezeRows(1);

                    wb.SaveAs(dlg.FileName);
                }
                finally { Mouse.OverrideCursor = null; }

                var result = MessageBox.Show(
                    $"Zapisano {dv.Count} wierszy do:\n{dlg.FileName}\n\nOtworzyć plik?",
                    "Eksport zakończony", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dlg.FileName) { UseShellExecute = true }); }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;
                MessageBox.Show("Błąd eksportu: " + ex.Message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ════════════════════ DWUKLIK → EDYCJA ════════════════════

        private void DgHistoria_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var dep = (DependencyObject)e.OriginalSource;
            while (dep != null && !(dep is DataGridRow))
                dep = VisualTreeHelper.GetParent(dep);

            if (dep is DataGridRow row && row.Item is DataRowView rowView)
            {
                var zamowienieId = rowView.Row.Field<int>("ZamowienieId");
                if (zamowienieId > 0)
                {
                    var win = new Kalendarz1.Zamowienia.Views.NoweZamowienieTestWindow(_userId, zamowienieId);
                    win.Owner = this;
                    if (win.ShowDialog() == true) _ = LoadDataAsync();
                }
            }
        }

        // ════════════════════ AVATAR — static cache + async load ════════════════════

        private void PreloadAvatars()
        {
            try
            {
                var uniqueIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (DataRow row in _dtHistoria.Rows)
                {
                    var uid = row["UzytkownikId"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(uid)) uniqueIds.Add(uid);
                }
                foreach (var uid in uniqueIds)
                {
                    if (s_avatarCache.ContainsKey(uid)) continue;
                    try
                    {
                        if (UserAvatarManager.HasAvatar(uid))
                        {
                            using var img = UserAvatarManager.GetAvatarRounded(uid, 32);
                            if (img != null)
                            {
                                var bs = ConvertToBitmapSource(img);
                                if (bs != null) s_avatarCache[uid] = bs;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void DgHistoria_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.DataContext is not DataRowView rowView) return;
            var uid = rowView.Row.Field<string>("UzytkownikId");
            if (string.IsNullOrEmpty(uid)) return;

            e.Row.Loaded += (s, args) =>
            {
                try
                {
                    var avatarImage = FindVisualChild<Ellipse>(e.Row, "avatarImage");
                    var avatarBorder = FindVisualChild<Border>(e.Row, "avatarBorder");
                    if (avatarImage == null || avatarBorder == null) return;

                    if (s_avatarCache.TryGetValue(uid, out var bs))
                    {
                        avatarImage.Fill = new ImageBrush(bs) { Stretch = Stretch.UniformToFill };
                        avatarImage.Visibility = Visibility.Visible;
                        avatarBorder.Visibility = Visibility.Collapsed;
                    }
                }
                catch { }
            };
        }

        private static BitmapSource? ConvertToBitmapSource(System.Drawing.Image image)
        {
            try
            {
                using var ms = new MemoryStream();
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                ms.Position = 0;
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.StreamSource = ms;
                bi.EndInit();
                bi.Freeze();
                return bi;
            }
            catch { return null; }
        }

        private T? FindVisualChild<T>(DependencyObject parent, string? name = null) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    if (name == null || (child is FrameworkElement fe && fe.Name == name))
                        return typedChild;
                }
                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
        }

        private void ShowLoader(bool visible, string? subText = null)
        {
            LoaderOverlay.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            if (subText != null) LoaderSubText.Text = subText;
            Mouse.OverrideCursor = visible ? Cursors.Wait : null;
            btnRefresh.IsEnabled = !visible;
            btnExport.IsEnabled = !visible;
        }
    }

    /// <summary>
    /// Konwerter do wyświetlania inicjałów z nazwy użytkownika
    /// </summary>
    public class InitialsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string name && !string.IsNullOrWhiteSpace(name))
            {
                var parts = name.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                    return (parts[0][0].ToString() + parts[1][0].ToString()).ToUpper();
                return name.Length >= 2 ? name.Substring(0, 2).ToUpper() : name.ToUpper();
            }
            return "?";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
