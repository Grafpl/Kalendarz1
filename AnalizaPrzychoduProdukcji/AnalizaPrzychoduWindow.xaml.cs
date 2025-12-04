using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace Kalendarz1.AnalizaPrzychoduProdukcji
{
    public partial class AnalizaPrzychoduWindow : Window, INotifyPropertyChanged
    {
        // Connection strings
        private readonly string _connLibra = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private readonly string _connHandel = "Server=192.168.0.112;Database=Handel;User Id=sa;Password=?cs_'Y6,n5#Xd'Yd;TrustServerCertificate=True";

        // Dane
        private List<PrzychodRecord> _wszystkieRekordy = new List<PrzychodRecord>();
        private List<PrzychodRecord> _przefiltrowaneDane = new List<PrzychodRecord>();

        // Słowniki
        private Dictionary<string, string> _towaryDict = new Dictionary<string, string>();
        private Dictionary<string, string> _operatorzyDict = new Dictionary<string, string>();
        private Dictionary<string, ArticleInfo> _articleDict = new Dictionary<string, ArticleInfo>();

        // Flaga do kontroli czy okno jest w pelni zaladowane
        private bool _isWindowFullyLoaded = false;

        // Wybrana partia dla szczegolowej analizy
        private string _wybranaPartia = null;

        // Binding properties dla wykresow
        private ChartValues<double> _przychodValues;
        public ChartValues<double> PrzychodValues
        {
            get => _przychodValues;
            set { _przychodValues = value; OnPropertyChanged(); }
        }

        private List<string> _przychodLabels;
        public List<string> PrzychodLabels
        {
            get => _przychodLabels;
            set { _przychodLabels = value; OnPropertyChanged(); }
        }

        private ChartValues<double> _operatorSumaValues;
        public ChartValues<double> OperatorSumaValues
        {
            get => _operatorSumaValues;
            set { _operatorSumaValues = value; OnPropertyChanged(); }
        }

        private List<string> _operatorLabels;
        public List<string> OperatorLabels
        {
            get => _operatorLabels;
            set { _operatorLabels = value; OnPropertyChanged(); }
        }

        // Bindingi dla nowych wykresow
        private List<string> _zmianyLabels;
        public List<string> ZmianyLabels
        {
            get => _zmianyLabels;
            set { _zmianyLabels = value; OnPropertyChanged(); }
        }

        private List<string> _terminalLabels;
        public List<string> TerminalLabels
        {
            get => _terminalLabels;
            set { _terminalLabels = value; OnPropertyChanged(); }
        }

        private List<string> _dniTygodniaLabels;
        public List<string> DniTygodniaLabels
        {
            get => _dniTygodniaLabels;
            set { _dniTygodniaLabels = value; OnPropertyChanged(); }
        }

        public Func<double, string> YFormatter { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public AnalizaPrzychoduWindow()
        {
            InitializeComponent();
            DataContext = this;

            YFormatter = value => value.ToString("N0") + " kg";
            PrzychodValues = new ChartValues<double>();
            PrzychodLabels = new List<string>();
            OperatorSumaValues = new ChartValues<double>();
            OperatorLabels = new List<string>();
            ZmianyLabels = new List<string> { "Poranna\n(5-13)", "Popoludniowa\n(13-21)", "Nocna\n(21-5)" };
            TerminalLabels = new List<string>();
            DniTygodniaLabels = new List<string> { "Pn", "Wt", "Sr", "Cz", "Pt", "So", "Nd" };

            InitializeFilters();

            // Zaladuj dane po pelnym zaladowaniu okna
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Ustaw flage ze okno jest w pelni zaladowane
            _isWindowFullyLoaded = true;

            // Laduj dane dopiero gdy okno jest w pelni zainicjalizowane
            // Uzyj Dispatcher.BeginInvoke z niskim priorytetem aby dac czas na inicjalizacje LiveCharts
            Dispatcher.BeginInvoke(new Action(() =>
            {
                LoadData();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        #region Inicjalizacja

        private void InitializeFilters()
        {
            // Domyslnie tylko data "do" aktywna (dzis)
            // Data "od" nieaktywna - wyszarzona
            dpDataOd.SelectedDate = DateTime.Today;
            dpDataDo.SelectedDate = DateTime.Today;

            // Domyslnie checkbox odznaczony - pojedynczy dzien
            chkOkresCzasu.IsChecked = false;
            dpDataOd.IsEnabled = false;
            dpDataOd.Opacity = 0.5;

            // Zaladuj slowniki
            LoadTowary();
            LoadOperatorzy();
            LoadTerminale();
            LoadPartie();
            LoadKlasyKurczaka();
            LoadGodziny();
            LoadArticles();

            // LoadData() jest teraz wywolywane w Window_Loaded
        }

        private void LoadTowary()
        {
            try
            {
                var towary = new List<ComboItemString> { new ComboItemString { Wartosc = "", Nazwa = "-- Wszystkie towary --" } };

                using (var conn = new SqlConnection(_connLibra))
                {
                    conn.Open();
                    // Pobierz towary z tabeli Article (dbo.Article)
                    string sql = @"SELECT ID, Name, ShortName
                                   FROM dbo.Article
                                   WHERE ID IS NOT NULL AND ID <> '' AND Name IS NOT NULL AND Name <> ''
                                   ORDER BY Name";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string id = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "";
                            string nazwa = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
                            string skrot = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(id))
                            {
                                string displayName = !string.IsNullOrEmpty(skrot) ? $"{skrot} - {nazwa}" : nazwa;
                                towary.Add(new ComboItemString { Wartosc = id, Nazwa = displayName });
                                _towaryDict[id] = nazwa;
                            }
                        }
                    }
                }

                cbTowar.ItemsSource = towary;
                cbTowar.DisplayMemberPath = "Nazwa";
                cbTowar.SelectedValuePath = "Wartosc";
                cbTowar.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Blad ladowania towarow: {ex.Message}";
            }
        }

        private void LoadOperatorzy()
        {
            try
            {
                var operatorzy = new List<ComboItemString> { new ComboItemString { Wartosc = "", Nazwa = "-- Wszyscy operatorzy --" } };

                using (var conn = new SqlConnection(_connLibra))
                {
                    conn.Open();
                    string sql = @"SELECT DISTINCT OperatorID, Wagowy
                                   FROM dbo.In0E
                                   WHERE OperatorID IS NOT NULL AND Wagowy IS NOT NULL AND Wagowy <> ''
                                   ORDER BY Wagowy";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string id = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "";
                            string nazwa = reader.IsDBNull(1) ? $"Operator {id}" : reader.GetValue(1)?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(id))
                            {
                                operatorzy.Add(new ComboItemString { Wartosc = id, Nazwa = nazwa });
                                _operatorzyDict[id] = nazwa;
                            }
                        }
                    }
                }

                cbOperator.ItemsSource = operatorzy;
                cbOperator.DisplayMemberPath = "Nazwa";
                cbOperator.SelectedValuePath = "Wartosc";
                cbOperator.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Blad ladowania operatorow: {ex.Message}";
            }
        }

        private void LoadTerminale()
        {
            try
            {
                var terminale = new List<ComboItem> { new ComboItem { Id = 0, Nazwa = "-- Wszystkie --" } };

                using (var conn = new SqlConnection(_connLibra))
                {
                    conn.Open();
                    string sql = @"SELECT DISTINCT TermID, TermType
                                   FROM dbo.In0E
                                   WHERE TermID IS NOT NULL
                                   ORDER BY TermID";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetValue(0));
                            string typ = reader.IsDBNull(1) ? $"T{id}" : reader.GetValue(1)?.ToString() ?? $"T{id}";
                            if (id > 0)
                            {
                                terminale.Add(new ComboItem { Id = id, Nazwa = typ });
                            }
                        }
                    }
                }

                cbTerminal.ItemsSource = terminale;
                cbTerminal.DisplayMemberPath = "Nazwa";
                cbTerminal.SelectedValuePath = "Id";
                cbTerminal.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Blad ladowania terminali: {ex.Message}";
            }
        }

        private void LoadPartie()
        {
            try
            {
                var partie = new List<ComboItemString> { new ComboItemString { Wartosc = "", Nazwa = "-- Wszystkie partie --" } };

                using (var conn = new SqlConnection(_connLibra))
                {
                    conn.Open();
                    string sql = @"SELECT DISTINCT TOP 100 P1
                                   FROM dbo.In0E
                                   WHERE P1 IS NOT NULL AND P1 <> ''
                                   ORDER BY P1 DESC";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string p1 = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(p1))
                            {
                                partie.Add(new ComboItemString { Wartosc = p1, Nazwa = p1 });
                            }
                        }
                    }
                }

                cbPartia.ItemsSource = partie;
                cbPartia.DisplayMemberPath = "Nazwa";
                cbPartia.SelectedValuePath = "Wartosc";
                cbPartia.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Blad ladowania partii: {ex.Message}";
            }
        }

        private void LoadKlasyKurczaka()
        {
            // Klasy dla ArticleID = 40 (QntInCont: 1, 2, 3, itd.)
            var klasy = new List<ComboItemString>
            {
                new ComboItemString { Wartosc = "", Nazwa = "-- Wszystkie --" },
                new ComboItemString { Wartosc = "1", Nazwa = "Klasa 1" },
                new ComboItemString { Wartosc = "2", Nazwa = "Klasa 2" },
                new ComboItemString { Wartosc = "3", Nazwa = "Klasa 3" },
                new ComboItemString { Wartosc = "4", Nazwa = "Klasa 4" },
                new ComboItemString { Wartosc = "5", Nazwa = "Klasa 5" },
                new ComboItemString { Wartosc = "6", Nazwa = "Klasa 6" },
                new ComboItemString { Wartosc = "7", Nazwa = "Klasa 7" },
                new ComboItemString { Wartosc = "8", Nazwa = "Klasa 8" },
                new ComboItemString { Wartosc = "9", Nazwa = "Klasa 9" },
                new ComboItemString { Wartosc = "10", Nazwa = "Klasa 10" },
                new ComboItemString { Wartosc = "11", Nazwa = "Klasa 11" },
                new ComboItemString { Wartosc = "12", Nazwa = "Klasa 12" }
            };

            cbKlasaKurczaka.ItemsSource = klasy;
            cbKlasaKurczaka.DisplayMemberPath = "Nazwa";
            cbKlasaKurczaka.SelectedValuePath = "Wartosc";
            cbKlasaKurczaka.SelectedIndex = 0;
        }

        private void LoadGodziny()
        {
            // Godziny od 0 do 23
            var godzinyOd = new List<ComboItem> { new ComboItem { Id = -1, Nazwa = "-- Od --" } };
            var godzinyDo = new List<ComboItem> { new ComboItem { Id = -1, Nazwa = "-- Do --" } };

            for (int h = 0; h <= 23; h++)
            {
                godzinyOd.Add(new ComboItem { Id = h, Nazwa = $"{h:D2}:00" });
                godzinyDo.Add(new ComboItem { Id = h, Nazwa = $"{h:D2}:59" });
            }

            cbGodzinaOd.ItemsSource = godzinyOd;
            cbGodzinaOd.DisplayMemberPath = "Nazwa";
            cbGodzinaOd.SelectedValuePath = "Id";
            cbGodzinaOd.SelectedIndex = 0;

            cbGodzinaDo.ItemsSource = godzinyDo;
            cbGodzinaDo.DisplayMemberPath = "Nazwa";
            cbGodzinaDo.SelectedValuePath = "Id";
            cbGodzinaDo.SelectedIndex = 0;
        }

        private void LoadArticles()
        {
            try
            {
                _articleDict.Clear();

                using (var conn = new SqlConnection(_connLibra))
                {
                    conn.Open();
                    string sql = @"SELECT ID, ShortName, Name
                                   FROM dbo.Article
                                   WHERE ID IS NOT NULL AND ID <> ''";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string id = reader.IsDBNull(0) ? "" : reader.GetValue(0)?.ToString() ?? "";
                            string shortName = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "";
                            string name = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "";

                            if (!string.IsNullOrEmpty(id) && !_articleDict.ContainsKey(id))
                            {
                                _articleDict[id] = new ArticleInfo
                                {
                                    ID = id,
                                    ShortName = shortName,
                                    Name = name
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Blad ladowania artykulow: {ex.Message}";
            }
        }

        #endregion

        #region Event Handlers

        private void CbTowar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Pokaz panel klasy kurczaka tylko dla ArticleID = 40
            if (cbTowar.SelectedValue is string towarId && towarId == "40")
            {
                pnlKlasaKurczaka.Visibility = Visibility.Visible;
            }
            else
            {
                pnlKlasaKurczaka.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnLoadData_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            // Odswierz filtry i zaladuj ponownie
            LoadTowary();
            LoadOperatorzy();
            LoadPartie();
            LoadData();
        }

        private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            ExportToExcel();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ChartType_Changed(object sender, RoutedEventArgs e)
        {
            if (_przefiltrowaneDane == null || !_przefiltrowaneDane.Any()) return;
            UpdateChartType();
        }

        private void ChkOkresCzasu_Changed(object sender, RoutedEventArgs e)
        {
            if (chkOkresCzasu == null || dpDataOd == null || lblDataOd == null) return;

            if (chkOkresCzasu.IsChecked == true)
            {
                // Aktywuj date "od"
                dpDataOd.IsEnabled = true;
                dpDataOd.Opacity = 1.0;
                lblDataOd.Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80)); // ciemny
                dpDataOd.SelectedDate = DateTime.Today.AddDays(-7); // Domyslnie tydzien wstecz
            }
            else
            {
                // Dezaktywuj date "od"
                dpDataOd.IsEnabled = false;
                dpDataOd.Opacity = 0.5;
                lblDataOd.Foreground = new SolidColorBrush(Color.FromRgb(149, 165, 166)); // szary
                dpDataOd.SelectedDate = dpDataDo.SelectedDate; // Ustaw ta sama date
            }

            // Auto-odswierz dane
            if (_isWindowFullyLoaded)
            {
                LoadData();
            }
        }

        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isWindowFullyLoaded) return;

            // Jezeli checkbox nie zaznaczony, synchronizuj daty
            if (chkOkresCzasu?.IsChecked != true && sender == dpDataDo)
            {
                dpDataOd.SelectedDate = dpDataDo.SelectedDate;
            }

            // Auto-odswierz dane
            LoadData();
        }

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
            if (dgPartie.SelectedItem is PartiaStats partia)
            {
                ShowWazeniaWindow(partia.Partia, null);
            }
        }

        private void DgPartieArtykuly_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgPartieArtykuly.SelectedItem is PartiaArticleStats artykul && !string.IsNullOrEmpty(_wybranaPartia))
            {
                ShowWazeniaWindow(_wybranaPartia, artykul.ArticleID);
            }
        }

        private void BtnPokazWszystkieWazenia_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_wybranaPartia))
            {
                ShowWazeniaWindow(_wybranaPartia, null);
            }
        }

        private void UpdatePartieDetails(string partia)
        {
            if (string.IsNullOrEmpty(partia))
            {
                dgPartieArtykuly.ItemsSource = null;
                txtPartiaHeader.Text = "SZCZEGOLY PARTII";
                txtPartiaInfo.Text = "Wybierz partie z listy";
                btnPokazWszystkieWazenia.Visibility = Visibility.Collapsed;
                return;
            }

            txtPartiaHeader.Text = $"PARTIA: {partia}";

            // Pobierz artykuly z tej partii
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
            {
                a.Procent = suma > 0 ? (a.SumaKg / suma * 100) : 0;
            }

            txtPartiaInfo.Text = $"{artykulyPartii.Count} artykulow, {suma:N2} kg lacznie";
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
                ? $"Wszystkie wazenia - Partia: {partia}"
                : $"Wazenia - Partia: {partia}, Artykul: {articleId}";

            // Utworz okno z lista wazen
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

            // Header
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                Padding = new Thickness(15, 10, 15, 10)
            };
            var headerText = new TextBlock
            {
                Text = $"{tytul} ({wazenia.Count} rekordow, {wazenia.Sum(w => w.ActWeight):N2} kg)",
                Foreground = Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };
            header.Child = headerText;
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // DataGrid
            var dg = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                Margin = new Thickness(10),
                HeadersVisibility = DataGridHeadersVisibility.Column,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                RowBackground = Brushes.White,
                AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 249, 250))
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

            // Footer z przyciskiem zamknij
            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(10)
            };
            var btnClose = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(20, 8, 20, 8),
                Background = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
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

        #region Ladowanie danych

        private void LoadData()
        {
            if (!dpDataOd.SelectedDate.HasValue || !dpDataDo.SelectedDate.HasValue)
            {
                MessageBox.Show("Wybierz zakres dat.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            txtStatus.Text = "Ladowanie danych...";
            _wszystkieRekordy.Clear();

            try
            {
                using (var conn = new SqlConnection(_connLibra))
                {
                    conn.Open();

                    // Glowne zapytanie - pobierz wszystkie dane z zakresu dat z tabeli In0E
                    string sql = @"
                        SELECT
                            ArticleID,
                            ArticleName,
                            JM,
                            TermID,
                            TermType,
                            Weight,
                            Quantity,
                            Direction,
                            Data,
                            Godzina,
                            OperatorID,
                            Wagowy,
                            Tara,
                            Price,
                            P1,
                            P2,
                            ActWeight,
                            QntInCont
                        FROM dbo.In0E
                        WHERE Data >= @DataOd AND Data <= @DataDo
                          AND ISNULL(ArticleName,'') <> ''
                        ORDER BY Data, Godzina";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dpDataOd.SelectedDate.Value.ToString("yyyy-MM-dd"));
                        cmd.Parameters.AddWithValue("@DataDo", dpDataDo.SelectedDate.Value.ToString("yyyy-MM-dd"));

                        using (var reader = cmd.ExecuteReader())
                        {
                            // Pobierz liczbe kolumn
                            int fieldCount = reader.FieldCount;

                            while (reader.Read())
                            {
                                // Parsowanie daty i godziny z varchar
                                DateTime dataValue = DateTime.MinValue;
                                DateTime godzinaValue = DateTime.MinValue;

                                string dataStr = (fieldCount > 8 && !reader.IsDBNull(8)) ? reader.GetValue(8)?.ToString() ?? "" : "";
                                string godzinaStr = (fieldCount > 9 && !reader.IsDBNull(9)) ? reader.GetValue(9)?.ToString() ?? "" : "";

                                DateTime.TryParse(dataStr, out dataValue);

                                // Godzina moze byc w formacie HH:mm:ss
                                if (!string.IsNullOrEmpty(godzinaStr))
                                {
                                    if (TimeSpan.TryParse(godzinaStr, out TimeSpan ts))
                                    {
                                        godzinaValue = dataValue.Date.Add(ts);
                                    }
                                    else
                                    {
                                        DateTime.TryParse(godzinaStr, out godzinaValue);
                                    }
                                }

                                var record = new PrzychodRecord
                                {
                                    ArticleID = (fieldCount > 0 && !reader.IsDBNull(0)) ? reader.GetValue(0)?.ToString() ?? "" : "",
                                    NazwaTowaru = (fieldCount > 1 && !reader.IsDBNull(1)) ? reader.GetValue(1)?.ToString() ?? "" : "",
                                    JM = (fieldCount > 2 && !reader.IsDBNull(2)) ? reader.GetValue(2)?.ToString() ?? "" : "",
                                    TermID = (fieldCount > 3 && !reader.IsDBNull(3)) ? Convert.ToInt32(reader.GetValue(3)) : 0,
                                    Terminal = (fieldCount > 4 && !reader.IsDBNull(4)) ? reader.GetValue(4)?.ToString() ?? "" : "",
                                    Weight = (fieldCount > 5 && !reader.IsDBNull(5)) ? Convert.ToDecimal(reader.GetValue(5)) : 0,
                                    Data = dataValue,
                                    Godzina = godzinaValue,
                                    OperatorID = (fieldCount > 10 && !reader.IsDBNull(10)) ? reader.GetValue(10)?.ToString() ?? "" : "",
                                    Operator = (fieldCount > 11 && !reader.IsDBNull(11)) ? reader.GetValue(11)?.ToString() ?? "" : "",
                                    Tara = (fieldCount > 12 && !reader.IsDBNull(12)) ? Convert.ToDecimal(reader.GetValue(12)) : 0,
                                    Partia = (fieldCount > 14 && !reader.IsDBNull(14)) ? reader.GetValue(14)?.ToString() ?? "" : "",
                                    ActWeight = (fieldCount > 16 && !reader.IsDBNull(16)) ? Convert.ToDecimal(reader.GetValue(16)) : 0,
                                    Klasa = (fieldCount > 17 && !reader.IsDBNull(17)) ? Convert.ToInt32(reader.GetValue(17)) : 0
                                };

                                _wszystkieRekordy.Add(record);
                            }
                        }
                    }
                }

                // Zastosuj filtry
                ApplyFilters();

                txtStatus.Text = $"Zaladowano {_wszystkieRekordy.Count} rekordow, po filtracji: {_przefiltrowaneDane.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad podczas ladowania danych:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Blad ladowania danych";
            }
        }

        private void ApplyFilters()
        {
            _przefiltrowaneDane = _wszystkieRekordy.ToList();

            // Filtr towaru
            if (cbTowar.SelectedValue is string towarId && !string.IsNullOrEmpty(towarId))
            {
                _przefiltrowaneDane = _przefiltrowaneDane.Where(r => r.ArticleID == towarId).ToList();
            }

            // Filtr operatora
            if (cbOperator.SelectedValue is string operatorId && !string.IsNullOrEmpty(operatorId))
            {
                _przefiltrowaneDane = _przefiltrowaneDane.Where(r => r.OperatorID == operatorId).ToList();
            }

            // Filtr terminala
            if (cbTerminal.SelectedValue is int terminalId && terminalId > 0)
            {
                _przefiltrowaneDane = _przefiltrowaneDane.Where(r => r.TermID == terminalId).ToList();
            }

            // Filtr partii
            if (cbPartia.SelectedValue is string partia && !string.IsNullOrEmpty(partia))
            {
                _przefiltrowaneDane = _przefiltrowaneDane.Where(r => r.Partia == partia).ToList();
            }

            // Filtr klasy kurczaka (tylko dla ArticleID = 40)
            if (cbTowar.SelectedValue is string tid && tid == "40")
            {
                if (cbKlasaKurczaka.SelectedValue is string klasaStr && !string.IsNullOrEmpty(klasaStr))
                {
                    if (int.TryParse(klasaStr, out int klasa))
                    {
                        _przefiltrowaneDane = _przefiltrowaneDane.Where(r => r.Klasa == klasa).ToList();
                    }
                }
            }

            // Filtr godziny od
            if (cbGodzinaOd.SelectedValue is int godzinaOd && godzinaOd >= 0)
            {
                _przefiltrowaneDane = _przefiltrowaneDane.Where(r => r.Godzina.Hour >= godzinaOd).ToList();
            }

            // Filtr godziny do
            if (cbGodzinaDo.SelectedValue is int godzinaDo && godzinaDo >= 0)
            {
                _przefiltrowaneDane = _przefiltrowaneDane.Where(r => r.Godzina.Hour <= godzinaDo).ToList();
            }

            // Aktualizuj wszystkie widoki
            UpdateStatistics();
            UpdateCharts();
            UpdateDataGrids();
            UpdateHeatmap();
        }

        #endregion

        #region Statystyki

        private void UpdateStatistics()
        {
            // Null checks for UI controls
            if (txtSumaKg == null) return;

            if (!_przefiltrowaneDane.Any())
            {
                txtSumaKg.Text = "0 kg";
                txtSredniaGodzina.Text = "0 kg/h";
                if (txtSredniaDzien != null) txtSredniaDzien.Text = "0 kg";
                return;
            }

            // Suma
            decimal sumaKg = _przefiltrowaneDane.Sum(r => r.ActWeight);
            txtSumaKg.Text = $"{sumaKg:N2} kg";

            // Liczba dni
            int liczbaDni = _przefiltrowaneDane.Select(r => r.Data.Date).Distinct().Count();

            // Grupowanie po godzinach
            var grupyGodzinowe = _przefiltrowaneDane
                .GroupBy(r => r.Godzina.Hour)
                .Select(g => new { Godzina = g.Key, Suma = g.Sum(r => r.ActWeight) })
                .OrderBy(g => g.Godzina)
                .ToList();

            if (grupyGodzinowe.Any())
            {
                // Srednia na godzine
                decimal sredniaGodzina = grupyGodzinowe.Average(g => g.Suma);
                txtSredniaGodzina.Text = $"{sredniaGodzina:N2} kg/h";
            }

            // Srednia na dzien
            if (txtSredniaDzien != null && liczbaDni > 0)
            {
                decimal sredniaDzien = sumaKg / liczbaDni;
                txtSredniaDzien.Text = $"{sredniaDzien:N0} kg";
            }
        }

        #endregion

        #region Wykresy

        private void UpdateCharts()
        {
            UpdatePrzychodChart();
            UpdateOperatorChart();
            UpdatePartieChart();
            UpdatePrzychodyArtykuly();
            UpdateZmianyChart();
            UpdateTerminaleChart();
            UpdateKlasyChart();
            UpdateDniTygodniaChart();
        }

        private void UpdatePrzychodChart()
        {
            if (!_przefiltrowaneDane.Any())
            {
                PrzychodValues = new ChartValues<double>();
                PrzychodLabels = new List<string>();
                return;
            }

            // Grupowanie wg wybranej opcji
            if (cbGrupowanie == null) return;
            var grupowanie = (cbGrupowanie.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Godzina";

            IEnumerable<KeyValuePair<string, decimal>> grupy;

            switch (grupowanie)
            {
                case "Operator":
                    grupy = _przefiltrowaneDane
                        .GroupBy(r => r.Operator)
                        .Select(g => new KeyValuePair<string, decimal>(g.Key, g.Sum(r => r.ActWeight)))
                        .OrderByDescending(g => g.Value);
                    break;

                case "Towar":
                    grupy = _przefiltrowaneDane
                        .GroupBy(r => r.NazwaTowaru)
                        .Select(g => new KeyValuePair<string, decimal>(
                            g.Key.Length > 25 ? g.Key.Substring(0, 25) + "..." : g.Key,
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
                        .GroupBy(r => r.Terminal)
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

                default: // Godzina
                    grupy = _przefiltrowaneDane
                        .GroupBy(r => r.Godzina.Hour)
                        .Select(g => new KeyValuePair<string, decimal>($"{g.Key:D2}:00", g.Sum(r => r.ActWeight)))
                        .OrderBy(g => g.Key);
                    break;
            }

            var listaGrup = grupy.ToList();
            PrzychodValues = new ChartValues<double>(listaGrup.Select(g => (double)g.Value));
            PrzychodLabels = listaGrup.Select(g => g.Key).ToList();

            UpdateChartType();
        }

        private void UpdateChartType()
        {
            if (!_isWindowFullyLoaded) return;
            if (chartPrzychod == null || !chartPrzychod.IsLoaded) return;
            if (rbWykresSlupkowy == null || rbWykresLiniowy == null || rbWykresObszarowy == null) return;

            try
            {
                chartPrzychod.Series.Clear();

                if (rbWykresSlupkowy.IsChecked == true)
                {
                    chartPrzychod.Series.Add(new ColumnSeries
                    {
                        Title = "Przychod (kg)",
                        Values = PrzychodValues,
                        Fill = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                        MaxColumnWidth = 50,
                        DataLabels = true,
                        LabelPoint = point => $"{point.Y:N0}"
                    });
                }
                else if (rbWykresLiniowy.IsChecked == true)
                {
                    chartPrzychod.Series.Add(new LineSeries
                    {
                        Title = "Przychod (kg)",
                        Values = PrzychodValues,
                        Stroke = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                        Fill = Brushes.Transparent,
                        PointGeometrySize = 10,
                        DataLabels = true,
                        LabelPoint = point => $"{point.Y:N0}"
                    });
                }
                else // Obszarowy
                {
                    chartPrzychod.Series.Add(new LineSeries
                    {
                        Title = "Przychod (kg)",
                        Values = PrzychodValues,
                        Stroke = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                        Fill = new SolidColorBrush(Color.FromArgb(100, 52, 152, 219)),
                        PointGeometrySize = 8,
                        DataLabels = true,
                        LabelPoint = point => $"{point.Y:N0}"
                    });
                }
            }
            catch (NullReferenceException)
            {
                // LiveCharts moze rzucic wyjatek gdy wewnetrzne komponenty nie sa jeszcze gotowe
                // Sprobuj ponownie za chwile
                Dispatcher.BeginInvoke(new Action(() => UpdateChartType()), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void UpdateOperatorChart()
        {
            if (dgRankingOperatorow == null) return;

            if (!_przefiltrowaneDane.Any())
            {
                OperatorSumaValues = new ChartValues<double>();
                OperatorLabels = new List<string>();
                dgRankingOperatorow.ItemsSource = null;
                return;
            }

            var grupyOperatorow = _przefiltrowaneDane
                .GroupBy(r => r.Operator)
                .Select(g => new OperatorStats
                {
                    Nazwa = g.Key,
                    SumaKg = g.Sum(r => r.ActWeight),
                    LiczbaWazen = g.Count(),
                    SredniaKg = g.Average(r => r.ActWeight)
                })
                .OrderByDescending(g => g.SumaKg)
                .ToList();

            // Ranking
            for (int i = 0; i < grupyOperatorow.Count; i++)
            {
                grupyOperatorow[i].Pozycja = i + 1;
            }

            OperatorSumaValues = new ChartValues<double>(grupyOperatorow.Select(g => (double)g.SumaKg));
            OperatorLabels = grupyOperatorow.Select(g => g.Nazwa).ToList();

            dgRankingOperatorow.ItemsSource = grupyOperatorow;
        }

        private void UpdatePartieChart()
        {
            if (!_isWindowFullyLoaded) return;
            if (dgPartie == null) return;

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
                    Liczba = g.Count()
                })
                .OrderByDescending(g => g.Partia)  // Sortowanie malejaco po nazwie partii
                .ToList();

            decimal suma = grupyPartii.Sum(p => p.SumaKg);
            int sumaWazen = grupyPartii.Sum(p => p.Liczba);
            foreach (var p in grupyPartii)
            {
                p.Procent = suma > 0 ? (p.SumaKg / suma * 100) : 0;
            }

            // Dodaj wiersz sumy na poczatku
            var sumRow = new PartiaStats
            {
                Partia = "*** RAZEM ***",
                SumaKg = suma,
                Procent = 100,
                Liczba = sumaWazen
            };
            grupyPartii.Insert(0, sumRow);

            dgPartie.ItemsSource = grupyPartii;
        }

        private void UpdatePrzychodyArtykuly()
        {
            if (!_isWindowFullyLoaded) return;
            if (dgPrzychodyArtykuly == null) return;

            if (!_przefiltrowaneDane.Any())
            {
                dgPrzychodyArtykuly.ItemsSource = null;
                return;
            }

            var grupyArtykulow = _przefiltrowaneDane
                .GroupBy(r => r.ArticleID)
                .Select(g =>
                {
                    // Pobierz dane artykulu ze slownika
                    string shortName = "";
                    string articleName = g.First().NazwaTowaru; // domyslnie z In0E

                    if (_articleDict.TryGetValue(g.Key, out ArticleInfo artInfo))
                    {
                        shortName = artInfo.ShortName;
                        if (!string.IsNullOrEmpty(artInfo.Name))
                            articleName = artInfo.Name;
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

            decimal suma = grupyArtykulow.Sum(a => a.SumaKg);
            int sumaWazen = grupyArtykulow.Sum(a => a.LiczbaWazen);
            foreach (var a in grupyArtykulow)
            {
                a.Procent = suma > 0 ? (a.SumaKg / suma * 100) : 0;
            }

            // Dodaj wiersz sumy na poczatku
            var sumRow = new ArticleStats
            {
                ArticleID = "",
                ShortName = "SUMA",
                ArticleName = "*** RAZEM ***",
                SumaKg = suma,
                Procent = 100,
                LiczbaWazen = sumaWazen,
                SredniaKg = sumaWazen > 0 ? suma / sumaWazen : 0
            };
            grupyArtykulow.Insert(0, sumRow);

            dgPrzychodyArtykuly.ItemsSource = grupyArtykulow;
        }

        private void UpdateZmianyChart()
        {
            if (!_isWindowFullyLoaded) return;
            if (chartZmiany == null || !chartZmiany.IsLoaded) return;

            // Zmiana poranna: 3:00 - 14:00
            // Zmiana popoludniowa: 14:01 - 00:00 (czyli 14-23 oraz 0-2)

            decimal sumaPoranna = _przefiltrowaneDane.Where(r => r.Godzina.Hour >= 3 && r.Godzina.Hour < 14).Sum(r => r.ActWeight);
            decimal sumaPopoludniowa = _przefiltrowaneDane.Where(r => r.Godzina.Hour >= 14 || r.Godzina.Hour < 3).Sum(r => r.ActWeight);

            int liczbaPoranna = _przefiltrowaneDane.Count(r => r.Godzina.Hour >= 3 && r.Godzina.Hour < 14);
            int liczbaPopoludniowa = _przefiltrowaneDane.Count(r => r.Godzina.Hour >= 14 || r.Godzina.Hour < 3);

            // Aktualizacja etykiet
            if (txtZmianaPoranna != null)
            {
                txtZmianaPoranna.Text = $"{sumaPoranna:N0} kg";
                txtZmianaPorannaSzt.Text = $"{liczbaPoranna} wazen";
            }
            if (txtZmianaPopoludniowa != null)
            {
                txtZmianaPopoludniowa.Text = $"{sumaPopoludniowa:N0} kg";
                txtZmianaPopoludniowaSzt.Text = $"{liczbaPopoludniowa} wazen";
            }
            // Ukryj zmiane nocna (nie ma jej w nowym podziale)
            if (txtZmianaNocna != null)
            {
                txtZmianaNocna.Text = "-";
                txtZmianaNocnaSzt.Text = "-";
            }

            // Najlepsza zmiana
            if (txtNajlepszaZmiana != null)
            {
                var max = new[] { ("Poranna (3-14)", sumaPoranna), ("Popoludniowa (14-3)", sumaPopoludniowa) }
                    .OrderByDescending(x => x.Item2).First();
                txtNajlepszaZmiana.Text = $"{max.Item1} ({max.Item2:N0} kg)";
            }

            try
            {
                chartZmiany.Series.Clear();

                // Wykres - tylko 2 zmiany
                chartZmiany.Series.Add(new ColumnSeries
                {
                    Title = "Suma kg",
                    Values = new ChartValues<double> { (double)sumaPoranna, (double)sumaPopoludniowa },
                    Fill = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                    MaxColumnWidth = 80,
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:N0}"
                });
            }
            catch (NullReferenceException)
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateZmianyChart()), System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void UpdateTerminaleChart()
        {
            if (!_isWindowFullyLoaded) return;
            if (chartTerminale == null || !chartTerminale.IsLoaded || dgTerminale == null) return;

            if (!_przefiltrowaneDane.Any())
            {
                dgTerminale.ItemsSource = null;
                return;
            }

            var grupyTerminali = _przefiltrowaneDane
                .GroupBy(r => string.IsNullOrEmpty(r.Terminal) ? $"T{r.TermID}" : r.Terminal)
                .Select(g => new TerminalStats
                {
                    Nazwa = g.Key,
                    SumaKg = g.Sum(r => r.ActWeight),
                    LiczbaWazen = g.Count(),
                    SredniaKg = g.Average(r => r.ActWeight)
                })
                .OrderByDescending(g => g.SumaKg)
                .ToList();

            // Ranking
            for (int i = 0; i < grupyTerminali.Count; i++)
            {
                grupyTerminali[i].Pozycja = i + 1;
            }

            TerminalLabels = grupyTerminali.Select(g => g.Nazwa).ToList();

            try
            {
                chartTerminale.Series.Clear();

                chartTerminale.Series.Add(new ColumnSeries
                {
                    Title = "Suma kg",
                    Values = new ChartValues<double>(grupyTerminali.Select(g => (double)g.SumaKg)),
                    Fill = new SolidColorBrush(Color.FromRgb(155, 89, 182)),
                    MaxColumnWidth = 60
                });
            }
            catch (NullReferenceException)
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateTerminaleChart()), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            dgTerminale.ItemsSource = grupyTerminali;
        }

        private void UpdateKlasyChart()
        {
            if (!_isWindowFullyLoaded) return;
            if (chartKlasy == null || !chartKlasy.IsLoaded || dgKlasy == null) return;

            // Filtruj tylko dane dla ArticleID = 40 (kurczaki)
            var daneKurczakow = _przefiltrowaneDane.Where(r => r.ArticleID == "40").ToList();

            if (!daneKurczakow.Any())
            {
                dgKlasy.ItemsSource = null;
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
            {
                k.Procent = suma > 0 ? (k.SumaKg / suma * 100) : 0;
            }

            try
            {
                chartKlasy.Series.Clear();

                // Wykres kolowy
                var kolory = new[] {
                    Color.FromRgb(231, 76, 60),   // Klasa 1 - czerwony
                    Color.FromRgb(230, 126, 34),  // Klasa 2 - pomaranczowy
                    Color.FromRgb(241, 196, 15),  // Klasa 3 - zolty
                    Color.FromRgb(46, 204, 113),  // Klasa 4 - zielony
                    Color.FromRgb(26, 188, 156),  // Klasa 5 - turkusowy
                    Color.FromRgb(52, 152, 219),  // Klasa 6 - niebieski
                    Color.FromRgb(155, 89, 182),  // Klasa 7 - fioletowy
                    Color.FromRgb(52, 73, 94),    // Klasa 8 - szary
                    Color.FromRgb(149, 165, 166), // Klasa 9
                    Color.FromRgb(189, 195, 199), // Klasa 10
                    Color.FromRgb(127, 140, 141), // Klasa 11
                    Color.FromRgb(44, 62, 80)     // Klasa 12
                };

                foreach (var klasa in grupyKlas)
                {
                    int kolorIndex = (klasa.Klasa - 1) % kolory.Length;
                    chartKlasy.Series.Add(new PieSeries
                    {
                        Title = $"Klasa {klasa.Klasa}",
                        Values = new ChartValues<double> { (double)klasa.SumaKg },
                        Fill = new SolidColorBrush(kolory[kolorIndex]),
                        DataLabels = true,
                        LabelPoint = point => $"Kl.{klasa.Klasa}: {klasa.Procent:N1}%"
                    });
                }
            }
            catch (NullReferenceException)
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateKlasyChart()), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            dgKlasy.ItemsSource = grupyKlas;
        }

        private void UpdateDniTygodniaChart()
        {
            if (!_isWindowFullyLoaded) return;
            if (chartDniTygodnia == null || !chartDniTygodnia.IsLoaded || dgDniTygodnia == null) return;

            if (!_przefiltrowaneDane.Any())
            {
                dgDniTygodnia.ItemsSource = null;
                return;
            }

            var nazwyDni = new[] { "Poniedzialek", "Wtorek", "Sroda", "Czwartek", "Piatek", "Sobota", "Niedziela" };

            var grupyDni = _przefiltrowaneDane
                .GroupBy(r => (int)r.Data.DayOfWeek)
                .Select(g => new DzienTygodniaStats
                {
                    DzienNumer = g.Key == 0 ? 7 : g.Key, // Niedziela jako 7
                    DzienTygodnia = nazwyDni[g.Key == 0 ? 6 : g.Key - 1],
                    SumaKg = g.Sum(r => r.ActWeight),
                    LiczbaDni = g.Select(r => r.Data.Date).Distinct().Count(),
                    SredniaKg = g.Sum(r => r.ActWeight) / g.Select(r => r.Data.Date).Distinct().Count()
                })
                .OrderBy(g => g.DzienNumer)
                .ToList();

            // Przygotuj wartosci dla wykresu (pn-nd)
            var wartosci = new double[7];
            foreach (var dzien in grupyDni)
            {
                int index = dzien.DzienNumer - 1;
                if (index >= 0 && index < 7)
                    wartosci[index] = (double)dzien.SredniaKg;
            }

            try
            {
                chartDniTygodnia.Series.Clear();

                chartDniTygodnia.Series.Add(new ColumnSeries
                {
                    Title = "Srednia kg/dzien",
                    Values = new ChartValues<double>(wartosci),
                    Fill = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                    MaxColumnWidth = 60
                });
            }
            catch (NullReferenceException)
            {
                Dispatcher.BeginInvoke(new Action(() => UpdateDniTygodniaChart()), System.Windows.Threading.DispatcherPriority.Background);
                return;
            }

            dgDniTygodnia.ItemsSource = grupyDni;
        }

        #endregion

        #region DataGrids

        private void UpdateDataGrids()
        {
            if (dgSzczegoly == null || dgPodsumowanieTowary == null || dgPodsumowanieDni == null) return;

            // Szczegolowe dane
            dgSzczegoly.ItemsSource = _przefiltrowaneDane
                .OrderByDescending(r => r.Data)
                .ThenByDescending(r => r.Godzina)
                .ToList();

            if (txtLiczbaWierszy != null)
                txtLiczbaWierszy.Text = $"Wyswietlono: {_przefiltrowaneDane.Count} rekordow";

            // Podsumowanie wg towaru
            var podsumowanieTowary = _przefiltrowaneDane
                .GroupBy(r => r.NazwaTowaru)
                .Select(g => new
                {
                    Nazwa = g.Key,
                    SumaKg = g.Sum(r => r.ActWeight),
                    Liczba = g.Count(),
                    Srednia = g.Average(r => r.ActWeight)
                })
                .OrderByDescending(g => g.SumaKg)
                .ToList();

            dgPodsumowanieTowary.ItemsSource = podsumowanieTowary;

            // Podsumowanie wg dnia
            var podsumowanieDni = _przefiltrowaneDane
                .GroupBy(r => r.Data.Date)
                .Select(g => new
                {
                    Data = g.Key,
                    DzienTygodnia = g.Key.ToString("dddd", new CultureInfo("pl-PL")),
                    SumaKg = g.Sum(r => r.ActWeight),
                    Liczba = g.Count()
                })
                .OrderByDescending(g => g.Data)
                .ToList();

            dgPodsumowanieDni.ItemsSource = podsumowanieDni;
        }

        #endregion

        #region Heatmap

        private void UpdateHeatmap()
        {
            if (!_isWindowFullyLoaded) return;
            if (icHeatmap == null) return;

            try
            {
                icHeatmap.Items.Clear();

                if (!_przefiltrowaneDane.Any())
                {
                    // Pokaz komunikat gdy brak danych
                    icHeatmap.Items.Add(new TextBlock
                    {
                        Text = "Brak danych do wyswietlenia mapy cieplnej",
                        FontSize = 14,
                        Foreground = Brushes.Gray,
                        Margin = new Thickness(20)
                    });
                    return;
                }

                // Pobierz dni
                var dni = _przefiltrowaneDane
                    .Select(r => r.Data.Date)
                    .Where(d => d != DateTime.MinValue)
                    .Distinct()
                    .OrderBy(d => d)
                    .Take(30) // Max 30 dni
                    .ToList();

                if (!dni.Any())
                {
                    icHeatmap.Items.Add(new TextBlock
                    {
                        Text = "Brak poprawnych dat w danych",
                        FontSize = 14,
                        Foreground = Brushes.Gray,
                        Margin = new Thickness(20)
                    });
                    return;
                }

                // Zakres godzin: 3:00 - 23:00
                int godzinaOd = 3;
                int godzinaDo = 23;

                // Oblicz sumy dla kazdej godziny i dnia
                var daneHeatmap = new Dictionary<(DateTime, int), decimal>();
                for (int h = godzinaOd; h <= godzinaDo; h++)
                {
                    foreach (var dzien in dni)
                    {
                        var suma = _przefiltrowaneDane
                            .Where(r => r.Data.Date == dzien && r.Godzina.Hour == h)
                            .Sum(r => r.ActWeight);
                        daneHeatmap[(dzien, h)] = suma;
                    }
                }

                // Oblicz srednia i odchylenie dla kazdej godziny (kolumny)
                var statystykiGodzin = new Dictionary<int, (decimal srednia, decimal min, decimal max)>();
                for (int h = godzinaOd; h <= godzinaDo; h++)
                {
                    var wartosci = dni.Select(d => daneHeatmap[(d, h)]).Where(v => v > 0).ToList();
                    if (wartosci.Any())
                    {
                        statystykiGodzin[h] = (wartosci.Average(), wartosci.Min(), wartosci.Max());
                    }
                    else
                    {
                        statystykiGodzin[h] = (0, 0, 0);
                    }
                }

                // Naglowek z godzinami
                var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
                headerPanel.Children.Add(new Border
                {
                    Width = 80,
                    Height = 30,
                    Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                    Child = new TextBlock
                    {
                        Text = "Data",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                });

                for (int h = godzinaOd; h <= godzinaDo; h++)
                {
                    headerPanel.Children.Add(new Border
                    {
                        Width = 50,
                        Height = 30,
                        Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(44, 62, 80)),
                        BorderThickness = new Thickness(1, 0, 0, 0),
                        Child = new TextBlock
                        {
                            Text = $"{h}:00",
                            Foreground = Brushes.White,
                            FontWeight = FontWeights.SemiBold,
                            FontSize = 10,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    });
                }
                icHeatmap.Items.Add(headerPanel);

                // Wiersze z danymi
                foreach (var dzien in dni)
                {
                    var rowPanel = new StackPanel { Orientation = Orientation.Horizontal };

                    // Kolumna z data
                    rowPanel.Children.Add(new Border
                    {
                        Width = 80,
                        Height = 28,
                        Background = new SolidColorBrush(Color.FromRgb(236, 240, 241)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Child = new TextBlock
                        {
                            Text = dzien.ToString("MM-dd ddd", new CultureInfo("pl-PL")),
                            FontSize = 10,
                            FontWeight = FontWeights.SemiBold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    });

                    // Kolumny z godzinami
                    for (int h = godzinaOd; h <= godzinaDo; h++)
                    {
                        var wartosc = daneHeatmap[(dzien, h)];
                        var stats = statystykiGodzin[h];

                        // Oblicz kolor na podstawie pozycji wzgledem sredniej
                        Color kolorTla;
                        if (wartosc == 0)
                        {
                            kolorTla = Color.FromRgb(245, 245, 245); // Szary dla braku danych
                        }
                        else if (stats.max == stats.min)
                        {
                            kolorTla = Color.FromRgb(241, 196, 15); // Zolty gdy wszystkie wartosci rowne
                        }
                        else
                        {
                            // Normalizuj wartosc do zakresu 0-1
                            double normalized = (double)(wartosc - stats.min) / (double)(stats.max - stats.min);

                            if (normalized >= 0.66)
                            {
                                // Zielony - powyzej normy
                                int intensity = (int)(155 + (normalized - 0.66) * 100 / 0.34);
                                kolorTla = Color.FromRgb((byte)(46), (byte)Math.Min(255, intensity + 49), (byte)(113));
                            }
                            else if (normalized >= 0.33)
                            {
                                // Zolty - norma
                                kolorTla = Color.FromRgb(241, 196, 15);
                            }
                            else
                            {
                                // Czerwony - ponizej normy
                                int intensity = (int)(76 + normalized * 155 / 0.33);
                                kolorTla = Color.FromRgb((byte)Math.Min(255, 180 + (int)((0.33 - normalized) * 75)), (byte)intensity, (byte)(60));
                            }
                        }

                        rowPanel.Children.Add(new Border
                        {
                            Width = 50,
                            Height = 28,
                            Background = new SolidColorBrush(kolorTla),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(189, 195, 199)),
                            BorderThickness = new Thickness(1, 0, 0, 1),
                            Child = new TextBlock
                            {
                                Text = wartosc > 0 ? $"{wartosc:N0}" : "-",
                                FontSize = 9,
                                FontWeight = wartosc > 0 ? FontWeights.SemiBold : FontWeights.Normal,
                                Foreground = wartosc == 0 ? Brushes.Gray : Brushes.Black,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        });
                    }

                    icHeatmap.Items.Add(rowPanel);
                }
            }
            catch (Exception ex)
            {
                icHeatmap.Items.Add(new TextBlock
                {
                    Text = $"Blad wyswietlania mapy cieplnej: {ex.Message}",
                    FontSize = 12,
                    Foreground = Brushes.Red,
                    Margin = new Thickness(20)
                });
            }
        }

        #endregion

        #region Export

        private void ExportToExcel()
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

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        // Naglowek
                        writer.WriteLine("Data;Godzina;Towar;Waga (kg);Operator;Partia;Terminal;Klasa;Tara");

                        // Dane
                        foreach (var r in _przefiltrowaneDane.OrderBy(r => r.Data).ThenBy(r => r.Godzina))
                        {
                            writer.WriteLine($"{r.Data:yyyy-MM-dd};{r.Godzina:HH:mm:ss};{r.NazwaTowaru};{r.ActWeight:N2};{r.Operator};{r.Partia};{r.Terminal};{r.Klasa};{r.Tara:N2}");
                        }

                        // Podsumowanie
                        writer.WriteLine();
                        writer.WriteLine("=== PODSUMOWANIE ===");
                        writer.WriteLine($"Suma kg;{_przefiltrowaneDane.Sum(r => r.ActWeight):N2}");
                        writer.WriteLine($"Liczba rekordow;{_przefiltrowaneDane.Count}");
                        writer.WriteLine($"Zakres dat;{dpDataOd.SelectedDate:yyyy-MM-dd} - {dpDataDo.SelectedDate:yyyy-MM-dd}");
                    }

                    MessageBox.Show($"Eksport zakonczony pomyslnie!\n\n{dialog.FileName}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Blad podczas eksportu:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }

    #region Modele danych

    public class PrzychodRecord
    {
        public string ArticleID { get; set; }
        public string NazwaTowaru { get; set; }
        public string JM { get; set; }
        public int TermID { get; set; }
        public string Terminal { get; set; }
        public decimal Weight { get; set; }
        public DateTime Data { get; set; }
        public DateTime Godzina { get; set; }
        public string OperatorID { get; set; }
        public string Operator { get; set; }
        public decimal Tara { get; set; }
        public string Partia { get; set; }
        public decimal ActWeight { get; set; }
        public int Klasa { get; set; }
    }

    public class ComboItem
    {
        public int Id { get; set; }
        public string Nazwa { get; set; }
    }

    public class ComboItemString
    {
        public string Wartosc { get; set; }
        public string Nazwa { get; set; }
    }

    public class OperatorStats
    {
        public int Pozycja { get; set; }
        public string Nazwa { get; set; }
        public decimal SumaKg { get; set; }
        public int LiczbaWazen { get; set; }
        public decimal SredniaKg { get; set; }
    }

    public class PartiaStats
    {
        public string Partia { get; set; }
        public decimal SumaKg { get; set; }
        public decimal Procent { get; set; }
        public int Liczba { get; set; }
    }

    public class TerminalStats
    {
        public int Pozycja { get; set; }
        public string Nazwa { get; set; }
        public decimal SumaKg { get; set; }
        public int LiczbaWazen { get; set; }
        public decimal SredniaKg { get; set; }
    }

    public class KlasaStats
    {
        public int Klasa { get; set; }
        public decimal SumaKg { get; set; }
        public decimal Procent { get; set; }
        public int Liczba { get; set; }
        public decimal SredniaKg { get; set; }
    }

    public class DzienTygodniaStats
    {
        public int DzienNumer { get; set; }
        public string DzienTygodnia { get; set; }
        public decimal SumaKg { get; set; }
        public decimal SredniaKg { get; set; }
        public int LiczbaDni { get; set; }
    }

    public class ArticleInfo
    {
        public string ID { get; set; }
        public string ShortName { get; set; }
        public string Name { get; set; }
    }

    public class ArticleStats
    {
        public string ArticleID { get; set; }
        public string ShortName { get; set; }
        public string ArticleName { get; set; }
        public decimal SumaKg { get; set; }
        public decimal Procent { get; set; }
        public int LiczbaWazen { get; set; }
        public decimal SredniaKg { get; set; }
    }

    public class PartiaArticleStats
    {
        public string ArticleID { get; set; }
        public string ArticleName { get; set; }
        public decimal SumaKg { get; set; }
        public decimal Procent { get; set; }
        public int LiczbaWazen { get; set; }
    }

    #endregion
}
