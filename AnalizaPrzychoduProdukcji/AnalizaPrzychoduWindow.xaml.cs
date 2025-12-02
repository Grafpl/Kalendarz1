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

            InitializeFilters();
        }

        #region Inicjalizacja

        private void InitializeFilters()
        {
            // Domyslne daty - poprzedni dzien
            dpDataOd.SelectedDate = DateTime.Today.AddDays(-1);
            dpDataDo.SelectedDate = DateTime.Today;

            // Zaladuj slowniki
            LoadTowary();
            LoadOperatorzy();
            LoadTerminale();
            LoadPartie();
            LoadKlasyKurczaka();

            // Automatycznie zaladuj dane przy starcie
            LoadData();
        }

        private void LoadTowary()
        {
            try
            {
                var towary = new List<ComboItemString> { new ComboItemString { Wartosc = "", Nazwa = "-- Wszystkie towary --" } };

                using (var conn = new SqlConnection(_connLibra))
                {
                    conn.Open();
                    // Pobierz unikalne towary z tabeli przychodu In0E
                    string sql = @"SELECT DISTINCT ArticleID, ArticleName
                                   FROM dbo.In0E
                                   WHERE ArticleID IS NOT NULL AND ArticleName IS NOT NULL AND ArticleName <> ''
                                   ORDER BY ArticleName";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string id = reader.GetString(0);
                            string nazwa = reader.GetString(1);
                            towary.Add(new ComboItemString { Wartosc = id, Nazwa = nazwa });
                            _towaryDict[id] = nazwa;
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
                            string id = reader.GetString(0);
                            string nazwa = reader.IsDBNull(1) ? $"Operator {id}" : reader.GetString(1);
                            operatorzy.Add(new ComboItemString { Wartosc = id, Nazwa = nazwa });
                            _operatorzyDict[id] = nazwa;
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
                            int id = Convert.ToInt32(reader.GetValue(0));
                            string typ = reader.IsDBNull(1) ? $"T{id}" : reader.GetString(1);
                            terminale.Add(new ComboItem { Id = id, Nazwa = typ });
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
                            string p1 = reader.GetString(0);
                            partie.Add(new ComboItemString { Wartosc = p1, Nazwa = p1 });
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
                            while (reader.Read())
                            {
                                // Parsowanie daty i godziny z varchar
                                DateTime dataValue = DateTime.MinValue;
                                DateTime godzinaValue = DateTime.MinValue;

                                string dataStr = reader.IsDBNull(8) ? "" : reader.GetString(8);
                                string godzinaStr = reader.IsDBNull(9) ? "" : reader.GetString(9);

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
                                    ArticleID = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                    NazwaTowaru = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    JM = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    TermID = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                    Terminal = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    Weight = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetValue(5)),
                                    Data = dataValue,
                                    Godzina = godzinaValue,
                                    OperatorID = reader.IsDBNull(10) ? "" : reader.GetString(10),
                                    Operator = reader.IsDBNull(11) ? "" : reader.GetString(11),
                                    Tara = reader.IsDBNull(12) ? 0 : Convert.ToDecimal(reader.GetValue(12)),
                                    Partia = reader.IsDBNull(14) ? "" : reader.GetString(14),
                                    ActWeight = reader.IsDBNull(16) ? 0 : Convert.ToDecimal(reader.GetValue(16)),
                                    Klasa = reader.IsDBNull(17) ? 0 : reader.GetInt32(17)
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
                txtNajlepszaGodzina.Text = "-";
                txtNajgorszaGodzina.Text = "-";
                txtNajlepszyOperator.Text = "-";
                txtLiczbaRekordow.Text = "0";
                txtLiczbaDni.Text = "0";
                return;
            }

            // Suma
            decimal sumaKg = _przefiltrowaneDane.Sum(r => r.ActWeight);
            txtSumaKg.Text = $"{sumaKg:N2} kg";

            // Liczba rekordow
            txtLiczbaRekordow.Text = _przefiltrowaneDane.Count.ToString("N0");

            // Liczba dni
            int liczbaDni = _przefiltrowaneDane.Select(r => r.Data.Date).Distinct().Count();
            txtLiczbaDni.Text = liczbaDni.ToString();

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

                // Najlepsza godzina
                var najlepsza = grupyGodzinowe.OrderByDescending(g => g.Suma).First();
                txtNajlepszaGodzina.Text = $"{najlepsza.Godzina}:00";
                txtNajlepszaGodzinaKg.Text = $"{najlepsza.Suma:N2} kg";

                // Najgorsza godzina (z danymi)
                var najgorsza = grupyGodzinowe.OrderBy(g => g.Suma).First();
                txtNajgorszaGodzina.Text = $"{najgorsza.Godzina}:00";
                txtNajgorszaGodzinaKg.Text = $"{najgorsza.Suma:N2} kg";
            }

            // Najlepszy operator
            var grupyOperatorow = _przefiltrowaneDane
                .GroupBy(r => r.Operator)
                .Select(g => new { Operator = g.Key, Suma = g.Sum(r => r.ActWeight) })
                .OrderByDescending(g => g.Suma)
                .ToList();

            if (grupyOperatorow.Any())
            {
                var najlepszy = grupyOperatorow.First();
                txtNajlepszyOperator.Text = najlepszy.Operator;
                txtNajlepszyOperatorKg.Text = $"{najlepszy.Suma:N2} kg";
            }
        }

        #endregion

        #region Wykresy

        private void UpdateCharts()
        {
            UpdatePrzychodChart();
            UpdateOperatorChart();
            UpdatePartieChart();
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
            if (chartPrzychod?.Series == null) return;
            if (rbWykresSlupkowy == null || rbWykresLiniowy == null || rbWykresObszarowy == null) return;
            chartPrzychod.Series.Clear();

            if (rbWykresSlupkowy.IsChecked == true)
            {
                chartPrzychod.Series.Add(new ColumnSeries
                {
                    Title = "Przychod (kg)",
                    Values = PrzychodValues,
                    Fill = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                    MaxColumnWidth = 50
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
                    PointGeometrySize = 10
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
                    PointGeometrySize = 8
                });
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
            if (chartPartie?.Series == null || dgPartie == null) return;
            chartPartie.Series.Clear();

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
                .OrderByDescending(g => g.SumaKg)
                .ToList();

            decimal suma = grupyPartii.Sum(p => p.SumaKg);
            foreach (var p in grupyPartii)
            {
                p.Procent = suma > 0 ? (p.SumaKg / suma * 100) : 0;
            }

            // Wykres kolowy
            var kolory = new[] {
                Color.FromRgb(52, 152, 219),
                Color.FromRgb(46, 204, 113),
                Color.FromRgb(155, 89, 182),
                Color.FromRgb(241, 196, 15),
                Color.FromRgb(230, 126, 34),
                Color.FromRgb(231, 76, 60),
                Color.FromRgb(26, 188, 156),
                Color.FromRgb(52, 73, 94)
            };

            int kolorIndex = 0;
            foreach (var partia in grupyPartii.Take(8))
            {
                chartPartie.Series.Add(new PieSeries
                {
                    Title = partia.Partia,
                    Values = new ChartValues<double> { (double)partia.SumaKg },
                    Fill = new SolidColorBrush(kolory[kolorIndex % kolory.Length]),
                    DataLabels = true,
                    LabelPoint = point => $"{partia.Partia}: {point.Y:N0} kg"
                });
                kolorIndex++;
            }

            dgPartie.ItemsSource = grupyPartii;
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
            if (dgHeatmap == null) return;

            if (!_przefiltrowaneDane.Any())
            {
                dgHeatmap.ItemsSource = null;
                return;
            }

            // Tworzymy macierz: wiersze = dni, kolumny = godziny (6-22)
            var dni = _przefiltrowaneDane
                .Select(r => r.Data.Date)
                .Distinct()
                .OrderBy(d => d)
                .Take(30) // Max 30 dni
                .ToList();

            var dt = new DataTable();
            dt.Columns.Add("Data", typeof(string));

            for (int h = 5; h <= 22; h++)
            {
                dt.Columns.Add($"{h:D2}:00", typeof(string));
            }

            foreach (var dzien in dni)
            {
                var row = dt.NewRow();
                row["Data"] = dzien.ToString("MM-dd ddd", new CultureInfo("pl-PL"));

                for (int h = 5; h <= 22; h++)
                {
                    var suma = _przefiltrowaneDane
                        .Where(r => r.Data.Date == dzien && r.Godzina.Hour == h)
                        .Sum(r => r.ActWeight);

                    row[$"{h:D2}:00"] = suma > 0 ? $"{suma:N0}" : "-";
                }

                dt.Rows.Add(row);
            }

            dgHeatmap.ItemsSource = dt.DefaultView;
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

    #endregion
}
