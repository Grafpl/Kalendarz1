using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Kalendarz1
{
    public partial class WstawienieWindow : Window
    {
        private const string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public string UserID { get; set; }
        public int SztWstawienia { get; set; }
        public string Dostawca { get; set; }
        public bool Modyfikacja { get; set; }
        public int LpWstawienia { get; set; }
        public DateTime DataWstawienia { get; set; }

        // Wczytane przy modyfikacji - umożliwia "zachowaj typ ceny bez zmiany"
        private string BiezacyTypUmowy { get; set; }
        private string BiezacyTypCeny { get; set; }
        public DaneOstatniegoDostarczonego DaneOstatniegoDostarczonego { get; set; }

        // Wynik zapisu - odczytywany przez okno macierzyste do toast-a
        public bool ZapisanoSukces => _zapisanoSukces;
        public bool ZapiszInfoAuta => _zapiszInfoAuta;
        public int ZapiszIloscNotatek => _zapiszIloscNotatek;
        private bool _zapisanoSukces = false;
        private bool _zapiszInfoAuta = false;
        private int _zapiszIloscNotatek = 0;

        private List<DostawaRow> dostawyRows = new List<DostawaRow>();
        private List<SeriaWstawienRow> seriaRows = new List<SeriaWstawienRow>();
        private int nextDostawaId = 1;
        private bool trybSerii = false;
        private bool isLoading = false;

        // Kalendarz dostaw - widok tygodniowy
        private int _currentWeekIndex = 0;
        private List<MiniWeekData> _weeksWithDeliveries = new List<MiniWeekData>();
        private Dictionary<DateTime, List<MiniDeliveryItem>> _deliveryDates = new(); // Data -> lista dostaw
        private const int MAX_DAILY_CAPACITY = 80000; // Maksymalna pojemność 80 000 szt./dzień
        private static readonly CultureInfo _plCulture = new CultureInfo("pl-PL");

        // Debounce dla UpdateMiniCalendarPreview - nie wykonuj na każdy keystroke,
        // czekaj 300ms po ostatniej zmianie
        private System.Windows.Threading.DispatcherTimer _miniCalendarDebounceTimer;

        public WstawienieWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WczytajDostawcow();

            if (Modyfikacja)
            {
                txtTrybFormularza.Text = "Modyfikacja";
                txtTrybFormularza.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E67E22"));
                WczytajDaneDoEdycji();
                panelSeria.Visibility = Visibility.Collapsed;
                chkSeria.IsEnabled = false;
                WczytajNotatki();
            }
            else
            {
                txtTrybFormularza.Text = "Nowe";
                dpDataWstawienia.SelectedDate = DateTime.Today;

                // ZMIANA: Sprawdź czy przekazano dane z innego okna
                if (!string.IsNullOrEmpty(Dostawca))
                {
                    cmbDostawca.SelectedItem = Dostawca;
                }

                if (SztWstawienia > 0)
                {
                    txtSztukiWstawienia.Text = SztWstawienia.ToString("0");
                }

                // ZMIANA: Jeśli przekazano dane ostatniego dostarczonego, skopiuj je
                if (DaneOstatniegoDostarczonego != null && DaneOstatniegoDostarczonego.Dostawy != null && DaneOstatniegoDostarczonego.Dostawy.Count > 0)
                {
                    foreach (var dostawa in DaneOstatniegoDostarczonego.Dostawy)
                    {
                        DodajDostawe(dostawa.Doba, dostawa.Waga, dostawa.SztPoj, dostawa.Sztuki, dostawa.Auta);
                    }
                }
                else
                {
                    // Domyślne dostawy jeśli nie ma danych z ostatniego
                    DodajDostawe(35, 2.1, 20);
                    DodajDostawe(42, 2.8, 16);
                }
            }
        }

        #region Seria Wstawień

        private void ChkSeria_Checked(object sender, RoutedEventArgs e)
        {
            // Sprawdź czy pierwsze wstawienie jest wypełnione
            if (dpDataWstawienia.SelectedDate == null || string.IsNullOrWhiteSpace(txtSztukiWstawienia.Text))
            {
                MessageBox.Show(
                    "Najpierw wypełnij datę i ilość sztuk pierwszego wstawienia!",
                    "Uwaga",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                chkSeria.IsChecked = false;
                return;
            }

            trybSerii = true;
            panelSeria.Visibility = Visibility.Visible;

            if (seriaRows.Count == 0)
                DodajWpisSerii();
        }

        private void ChkSeria_Unchecked(object sender, RoutedEventArgs e)
        {
            trybSerii = false;
            panelSeria.Visibility = Visibility.Collapsed;
        }

        private void BtnDodajSerie_Click(object sender, RoutedEventArgs e)
        {
            DodajWpisSerii();
        }

        private void DodajWpisSerii()
        {
            // Seria zaczyna się od #2 bo #1 jest w panelJednePodstawy
            var seria = new SeriaWstawienRow { Id = seriaRows.Count + 2 };
            var grid = CreateSeriaGrid(seria);
            seriaRows.Add(seria);
            stackPanelSeria.Children.Add(grid);
        }

        private Grid CreateSeriaGrid(SeriaWstawienRow seria)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 6), Tag = seria };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            var txtNr = new TextBlock
            {
                Text = seria.Id.ToString(),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetColumn(txtNr, 0);
            grid.Children.Add(txtNr);

            var dp = new DatePicker
            {
                SelectedDate = DateTime.Today,
                Style = (Style)FindResource("ModernDatePicker")
            };
            Grid.SetColumn(dp, 1);
            grid.Children.Add(dp);
            seria.DpData = dp;

            var txt = new TextBox
            {
                Style = (Style)FindResource("ModernTextBox")
            };
            Grid.SetColumn(txt, 2);
            grid.Children.Add(txt);
            seria.TxtIlosc = txt;

            var btn = new Button
            {
                Content = "🗑️",
                Width = 32,
                Height = 28,
                Style = (Style)FindResource("DeleteButton")
            };
            btn.Click += (s, e) => UsunWpisSerii(seria);
            Grid.SetColumn(btn, 3);
            grid.Children.Add(btn);

            return grid;
        }

        private void UsunWpisSerii(SeriaWstawienRow seria)
        {
            // Można usunąć wszystkie - wstawienie #1 jest w panelJednePodstawy
            Grid gridToRemove = null;
            foreach (Grid grid in stackPanelSeria.Children)
            {
                if (grid.Tag == seria)
                {
                    gridToRemove = grid;
                    break;
                }
            }

            if (gridToRemove != null)
            {
                stackPanelSeria.Children.Remove(gridToRemove);
                seriaRows.Remove(seria);
                OdswiezNumeracjeSerii();
            }
        }

        private void OdswiezNumeracjeSerii()
        {
            // Seria zaczyna się od #2 (bo #1 jest w panelJednePodstawy)
            for (int i = 0; i < seriaRows.Count; i++)
            {
                seriaRows[i].Id = i + 2;

                // Znajdź grid i zaktualizuj numer
                foreach (Grid grid in stackPanelSeria.Children)
                {
                    if (grid.Tag == seriaRows[i])
                    {
                        var txtNr = grid.Children[0] as TextBlock;
                        if (txtNr != null)
                            txtNr.Text = (i + 2).ToString();
                        break;
                    }
                }
            }
        }

        private void BtnWklejWszystkieSztuki_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSztukiWstawienia.Text))
            {
                MessageBox.Show("Najpierw wpisz ilość sztuk wstawienia!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtSztukiWstawienia.Text, out int ilosc))
            {
                MessageBox.Show("Nieprawidłowa wartość w polu sztuk!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int wklejone = 0;
            foreach (var seria in seriaRows)
            {
                if (seria.TxtIlosc != null)
                {
                    seria.TxtIlosc.Text = ilosc.ToString();
                    wklejone++;
                }
            }

            if (wklejone > 0)
            {
                MessageBox.Show($"✅ Wklejono {ilosc:N0} sztuk do {wklejone} wstawień serii",
                               "Sukces",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Brak wstawień w serii!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Zarządzanie Dostawami

        private void BtnDodajDostawe_Click(object sender, RoutedEventArgs e)
        {
            DodajDostawe();
        }

        private void DodajDostawe(int? domyslnaDoba = null, double? domyslnaWaga = null, int? domyslneSztPoj = null, int? domyslneSztuki = null, int? domyslneAuta = null)
        {
            // Jeśli są już jakieś dostawy i nie podano domyślnych wartości, skopiuj wagę i szt/poj z ostatniej
            if (dostawyRows.Count > 0 && !domyslnaWaga.HasValue && !domyslneSztPoj.HasValue)
            {
                var ostatniaDostawa = dostawyRows[dostawyRows.Count - 1];

                // Skopiuj wagę z ostatniej dostawy
                if (ostatniaDostawa.TxtWaga != null && !string.IsNullOrWhiteSpace(ostatniaDostawa.TxtWaga.Text))
                {
                    if (double.TryParse(ostatniaDostawa.TxtWaga.Text.Replace(',', '.'),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double waga))
                    {
                        domyslnaWaga = waga;
                    }
                }

                // Skopiuj szt/poj z ostatniej dostawy
                if (ostatniaDostawa.TxtSztPoj != null && !string.IsNullOrWhiteSpace(ostatniaDostawa.TxtSztPoj.Text))
                {
                    if (int.TryParse(ostatniaDostawa.TxtSztPoj.Text, out int sztPoj))
                    {
                        domyslneSztPoj = sztPoj;
                    }
                }
            }

            var dostawa = new DostawaRow
            {
                Id = nextDostawaId++,
                Doba = domyslnaDoba,
                Waga = domyslnaWaga,
                SztPoj = domyslneSztPoj,
                Sztuki = domyslneSztuki,
                Auta = domyslneAuta
            };

            var grid = CreateDostawaGrid(dostawa);
            dostawyRows.Add(dostawa);
            stackPanelDostawy.Children.Add(grid);
            OdswiezNumeracjeDostawy();
        }

        private Grid CreateDostawaGrid(DostawaRow dostawa)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 5), Tag = dostawa };

            var widths = new[] { 30, 55, 110, 45, 55, 55, 55, 70, 55, 60, 80 };
            foreach (var width in widths)
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });

            // Nr
            var txtNr = new TextBlock { Text = dostawa.Id.ToString(), FontSize = 11, FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(txtNr, 0);
            grid.Children.Add(txtNr);
            dostawa.TxtNr = txtNr;

            // Doba
            var txtDoba = new TextBox { Style = (Style)FindResource("ModernTextBox"), FontSize = 11 };
            if (dostawa.Doba.HasValue) txtDoba.Text = dostawa.Doba.Value.ToString();
            txtDoba.TextChanged += (s, e) => PrzeliczenieDoby(dostawa);
            Grid.SetColumn(txtDoba, 1);
            grid.Children.Add(txtDoba);
            dostawa.TxtDoba = txtDoba;

            // Data
            var dpData = new DatePicker { Style = (Style)FindResource("ModernDatePicker"), FontSize = 11 };
            dpData.SelectedDateChanged += (s, e) => { PrzeliczenieRoznicyDni(dostawa); ScheduleMiniCalendarRefresh(); };
            Grid.SetColumn(dpData, 2);
            grid.Children.Add(dpData);
            dostawa.DpData = dpData;

            // Dni
            var txtDni = new TextBox { Style = (Style)FindResource("ReadOnlyTextBox"), FontSize = 11 };
            Grid.SetColumn(txtDni, 3);
            grid.Children.Add(txtDni);
            dostawa.TxtDni = txtDni;

            // Waga
            var txtWaga = new TextBox { Style = (Style)FindResource("ModernTextBox"), FontSize = 11 };
            if (dostawa.Waga.HasValue) txtWaga.Text = dostawa.Waga.Value.ToString("0.0");
            Grid.SetColumn(txtWaga, 4);
            grid.Children.Add(txtWaga);
            dostawa.TxtWaga = txtWaga;

            // Szt/poj
            var txtSztPoj = new TextBox { Style = (Style)FindResource("ModernTextBox"), FontSize = 11 };
            if (dostawa.SztPoj.HasValue) txtSztPoj.Text = dostawa.SztPoj.Value.ToString();
            txtSztPoj.TextChanged += (s, e) => ObliczAutaWyliczone(dostawa);
            Grid.SetColumn(txtSztPoj, 5);
            grid.Children.Add(txtSztPoj);
            dostawa.TxtSztPoj = txtSztPoj;

            // Mnożnik
            var txtMnoznik = new TextBox { Style = (Style)FindResource("HighlightTextBox"), FontSize = 11 };
            txtMnoznik.TextChanged += (s, e) => PrzeliczenieZMnoznika(dostawa);
            Grid.SetColumn(txtMnoznik, 6);
            grid.Children.Add(txtMnoznik);
            dostawa.TxtMnoznik = txtMnoznik;

            // Sztuki
            var txtSztuki = new TextBox { Style = (Style)FindResource("ModernTextBox"), FontSize = 11 };
            if (dostawa.Sztuki.HasValue) txtSztuki.Text = dostawa.Sztuki.Value.ToString();
            txtSztuki.TextChanged += (s, e) => { ObliczSumeSztuk(); ObliczAutaWyliczone(dostawa); ScheduleMiniCalendarRefresh(); };
            Grid.SetColumn(txtSztuki, 7);
            grid.Children.Add(txtSztuki);
            dostawa.TxtSztuki = txtSztuki;

            // Auta (RĘCZNE)
            var txtAutaReczne = new TextBox { Style = (Style)FindResource("HighlightTextBox"), FontSize = 11 };
            if (dostawa.Auta.HasValue) txtAutaReczne.Text = dostawa.Auta.Value.ToString();
            Grid.SetColumn(txtAutaReczne, 8);
            grid.Children.Add(txtAutaReczne);
            dostawa.TxtAutaReczne = txtAutaReczne;

            // Auto Wyliczone
            var txtAutaWyliczone = new TextBox { Style = (Style)FindResource("ReadOnlyTextBox"), FontSize = 11 };
            Grid.SetColumn(txtAutaWyliczone, 9);
            grid.Children.Add(txtAutaWyliczone);
            dostawa.TxtAutaWyliczone = txtAutaWyliczone;

            // Akcje
            var actionsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var btnWklej = new Button { Content = "📋", Width = 32, Height = 28, FontSize = 11, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(0, 0, 3, 0) };
            btnWklej.Click += (s, e) => WklejRoznice(dostawa);
            actionsPanel.Children.Add(btnWklej);

            var btnUsun = new Button { Content = "🗑️", Width = 32, Height = 28, Style = (Style)FindResource("DeleteButton") };
            btnUsun.Click += (s, e) => UsunDostawe(dostawa);
            actionsPanel.Children.Add(btnUsun);

            Grid.SetColumn(actionsPanel, 10);
            grid.Children.Add(actionsPanel);

            return grid;
        }

        private void UsunDostawe(DostawaRow dostawa)
        {
            if (dostawyRows.Count <= 1)
            {
                MessageBox.Show("Musisz mieć przynajmniej jedną dostawę!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show("Usunąć dostawę?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                Grid gridToRemove = null;
                foreach (Grid grid in stackPanelDostawy.Children)
                {
                    if (grid.Tag == dostawa)
                    {
                        gridToRemove = grid;
                        break;
                    }
                }

                if (gridToRemove != null)
                {
                    stackPanelDostawy.Children.Remove(gridToRemove);
                    dostawyRows.Remove(dostawa);
                    OdswiezNumeracjeDostawy();
                    ObliczSumeSztuk();
                }
            }
        }

        private void OdswiezNumeracjeDostawy()
        {
            for (int i = 0; i < dostawyRows.Count; i++)
            {
                dostawyRows[i].Id = i + 1;
                if (dostawyRows[i].TxtNr != null)
                    dostawyRows[i].TxtNr.Text = (i + 1).ToString();
            }
        }

        private void BtnWklejRozniceDoWszystkich_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSztukiRoznica.Text))
            {
                MessageBox.Show("Brak wartości różnicy do wklejenia!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string roznicaText = txtSztukiRoznica.Text.Replace(",", "").Replace(" ", "");

            if (!double.TryParse(roznicaText, out double roznica))
            {
                MessageBox.Show("Nieprawidłowa wartość różnicy!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int wklejone = 0;
            foreach (var dostawa in dostawyRows)
            {
                if (dostawa.TxtSztuki != null)
                {
                    dostawa.TxtSztuki.Text = roznica.ToString("0");
                    wklejone++;
                }
            }

            if (wklejone > 0)
            {
                MessageBox.Show($"✅ Wklejono {roznica:N0} sztuk do {wklejone} dostaw",
                               "Sukces",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
            }
        }

        #endregion

        #region Ładowanie danych

        // Cache statyczny - lista dostawców rzadko się zmienia, ładujemy raz na sesję aplikacji
        private static List<string> _dostawcyCache = null;
        private static DateTime _dostawcyCacheTime = DateTime.MinValue;
        private const int DOSTAWCY_CACHE_TTL_MIN = 30;

        private void WczytajDostawcow()
        {
            try
            {
                bool cacheSwiezy = _dostawcyCache != null
                    && (DateTime.Now - _dostawcyCacheTime).TotalMinutes < DOSTAWCY_CACHE_TTL_MIN;

                if (!cacheSwiezy)
                {
                    var lista = new List<string>();
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = "SELECT DISTINCT Name FROM dbo.DOSTAWCY ORDER BY Name";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                                lista.Add(reader["Name"].ToString());
                        }
                    }
                    _dostawcyCache = lista;
                    _dostawcyCacheTime = DateTime.Now;
                }

                cmbDostawca.Items.Clear();
                foreach (var name in _dostawcyCache)
                    cmbDostawca.Items.Add(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania dostawców: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajDaneDostawcy(string dostawca)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT Address, PostalCode, City, Distance, Phone1, Email 
                                   FROM dbo.Dostawcy 
                                   WHERE Shortname = @D OR Name = @D";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@D", dostawca);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string adres = $"{reader["Address"]}, {reader["PostalCode"]} {reader["City"]}";
                                txtAdresDostawcy.Text = adres;
                                txtKmDostawcy.Text = reader.IsDBNull(3) ? "-" : $"{reader["Distance"]} km";
                                txtTelefonDostawcy.Text = reader.IsDBNull(4) ? "-" : reader["Phone1"].ToString();
                                txtEmailDostawcy.Text = reader.IsDBNull(5) ? "-" : reader["Email"].ToString();
                                txtKmH.Text = reader.IsDBNull(3) ? "" : reader["Distance"].ToString();

                                // Panel jest ukryty - nie pokazuj info o hodowcy
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych dostawcy: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajNotatki()
        {
            if (!Modyfikacja) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT Uwagi, TypUmowy, TypCeny FROM dbo.WstawieniaKurczakow WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", LpWstawienia);
                        using (var r = cmd.ExecuteReader())
                        {
                            if (r.Read())
                            {
                                if (r["Uwagi"] != DBNull.Value) txtNotatki.Text = r["Uwagi"].ToString();
                                if (r["TypUmowy"] != DBNull.Value) BiezacyTypUmowy = r["TypUmowy"].ToString();
                                if (r["TypCeny"] != DBNull.Value) BiezacyTypCeny = r["TypCeny"].ToString();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania notatek: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajDaneDoEdycji()
        {
            if (!Modyfikacja) return;

            try
            {
                isLoading = true;

                txtSztukiWstawienia.Text = SztWstawienia.ToString("0");
                cmbDostawca.SelectedItem = Dostawca;
                dpDataWstawienia.SelectedDate = DataWstawienia;

                var harmonogram = PobierzHarmonogramDostaw(LpWstawienia);
                if (harmonogram.Count == 0)
                {
                    MessageBox.Show("Nie znaleziono dostaw dla tego wstawienia.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                stackPanelDostawy.Children.Clear();
                dostawyRows.Clear();
                nextDostawaId = 1;

                foreach (var dostawa in harmonogram)
                {
                    var dostawaRow = new DostawaRow { Id = nextDostawaId++, LpDostawy = dostawa.Lp };
                    var grid = CreateDostawaGrid(dostawaRow);
                    dostawyRows.Add(dostawaRow);
                    stackPanelDostawy.Children.Add(grid);

                    if (dostawaRow.DpData != null)
                        dostawaRow.DpData.SelectedDate = dostawa.DataOdbioru;

                    if (dostawaRow.TxtSztuki != null)
                        dostawaRow.TxtSztuki.Text = dostawa.SztukiDek.ToString("0");

                    if (dostawaRow.TxtWaga != null && dostawa.WagaDek > 0)
                        dostawaRow.TxtWaga.Text = dostawa.WagaDek.ToString("0.0");

                    if (dostawaRow.TxtSztPoj != null && dostawa.SztSzuflada > 0)
                        dostawaRow.TxtSztPoj.Text = dostawa.SztSzuflada.ToString("0");

                    if (dostawaRow.TxtAutaReczne != null && dostawa.Auta > 0)
                        dostawaRow.TxtAutaReczne.Text = dostawa.Auta.ToString("0");

                    if (dpDataWstawienia.SelectedDate.HasValue && dostawaRow.DpData.SelectedDate.HasValue)
                    {
                        int dni = (dostawaRow.DpData.SelectedDate.Value - dpDataWstawienia.SelectedDate.Value).Days;
                        if (dostawaRow.TxtDoba != null)
                            dostawaRow.TxtDoba.Text = dni.ToString();
                        if (dostawaRow.TxtDni != null)
                            dostawaRow.TxtDni.Text = dni.ToString();
                    }
                }

                OdswiezNumeracjeDostawy();
                isLoading = false;

                // Upadki nie są liczone przez TextChanged (był isLoading=true) - oblicz ręcznie
                if (double.TryParse(txtSztukiWstawienia.Text, out double sw))
                    txtSztukiUpadki.Text = (sw * 0.97).ToString("0");

                ObliczSumeSztuk();

                foreach (var d in dostawyRows)
                    ObliczAutaWyliczone(d);
            }
            catch (Exception ex)
            {
                isLoading = false;
                MessageBox.Show($"Błąd wczytywania danych: {ex.Message}\n\nStack: {ex.StackTrace}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<HarmonogramDostaw> PobierzHarmonogramDostaw(int lpw)
        {
            var lista = new List<HarmonogramDostaw>();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT 
                                        CAST(Lp AS BIGINT) AS Lp, 
                                        dataodbioru, 
                                        ISNULL(sztukidek, 0) AS sztukidek, 
                                        ISNULL(WagaDek, 0) AS WagaDek, 
                                        ISNULL(SztSzuflada, 0) AS SztSzuflada,
                                        ISNULL(Auta, 0) AS Auta
                                   FROM dbo.HarmonogramDostaw 
                                   WHERE LpW = @LpW 
                                   ORDER BY dataodbioru";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@LpW", lpw);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                lista.Add(new HarmonogramDostaw
                                {
                                    Lp = reader.GetInt64(0),
                                    DataOdbioru = reader.GetDateTime(1),
                                    SztukiDek = reader.GetInt32(2),
                                    WagaDek = reader.GetDecimal(3),
                                    SztSzuflada = reader.GetInt32(4),
                                    Auta = reader.GetInt32(5)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd pobierania harmonogramu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return lista;
        }

        private bool SprawdzPodobneWstawienie(DateTime data, string dostawca)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT TOP 1 Lp, DataWstawienia, IloscWstawienia, KtoStwo, DataUtw
                                   FROM dbo.WstawieniaKurczakow
                                   WHERE Dostawca = @Dostawca 
                                   AND DataWstawienia BETWEEN DATEADD(day, -1, @Data) AND DATEADD(day, 1, @Data)
                                   ORDER BY DataWstawienia DESC";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Dostawca", dostawca);
                        cmd.Parameters.AddWithValue("@Data", data);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                long lp = reader.GetInt64(0);
                                DateTime dataWst = reader.GetDateTime(1);
                                int ilosc = reader.GetInt32(2);
                                string kto = reader.IsDBNull(3) ? "Nieznany" : reader.GetString(3);
                                DateTime dataUtw = reader.IsDBNull(4) ? DateTime.MinValue : reader.GetDateTime(4);

                                var result = MessageBox.Show(
                                    $"⚠️ UWAGA! Znaleziono podobne wstawienie:\n\n" +
                                    $"📋 LP: {lp}\n" +
                                    $"📅 Data: {dataWst:yyyy-MM-dd}\n" +
                                    $"🐣 Ilość: {ilosc:N0} sztuk\n" +
                                    $"👤 Utworzył: {kto}\n" +
                                    $"🕐 Kiedy: {dataUtw:yyyy-MM-dd HH:mm}\n\n" +
                                    $"Czy na pewno chcesz utworzyć nowe wstawienie?",
                                    "Podobne Wstawienie",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Warning);

                                return result == MessageBoxResult.No;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd sprawdzania podobnych wstawień: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
        }

        #endregion

        #region Obsługa zmian

        private void CmbDostawca_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbDostawca.SelectedItem != null)
            {
                string nazwaDostawcy = cmbDostawca.SelectedItem.ToString();
                txtNazwaDostawcy.Text = nazwaDostawcy;
                WczytajDaneDostawcy(nazwaDostawcy);
            }
        }

        private void DpDataWstawienia_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpDataWstawienia.SelectedDate == null || isLoading) return;

            // Doba jest wiążąca: zmiana daty wstawienia przesuwa daty dostaw o offset z TxtDoba.
            // Wstawienie 1.01 + Doba 1 → DpData 2.01. Po zmianie wstawienia na 2.01 → DpData 3.01.
            foreach (var dostawa in dostawyRows)
            {
                PrzeliczenieDoby(dostawa);     // TxtDoba (offset) → DpData
                PrzeliczenieRoznicyDni(dostawa); // odśwież TxtDni
            }
        }

        private void TxtSztukiWstawienia_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isLoading && double.TryParse(txtSztukiWstawienia.Text, out double value))
            {
                txtSztukiUpadki.Text = (value * 0.97).ToString("0");
                ObliczSumeSztuk();
            }
        }

        #endregion

        #region Obliczenia

        private void ObliczSumeSztuk()
        {
            if (isLoading) return;

            double suma = 0;
            foreach (var dostawa in dostawyRows)
            {
                if (dostawa.TxtSztuki != null && double.TryParse(dostawa.TxtSztuki.Text, out double val))
                    suma += val;
            }
            txtSztukiSuma.Text = suma.ToString("0");

            if (double.TryParse(txtSztukiUpadki.Text, out double upadki))
            {
                double roznica = upadki - suma;
                txtSztukiRoznica.Text = roznica.ToString("0");
            }
        }

        private void PrzeliczenieDoby(DostawaRow dostawa)
        {
            if (isLoading || dpDataWstawienia.SelectedDate == null || dostawa.TxtDoba == null || dostawa.DpData == null) return;
            if (int.TryParse(dostawa.TxtDoba.Text, out int dni))
                dostawa.DpData.SelectedDate = dpDataWstawienia.SelectedDate.Value.AddDays(dni);
        }

        private void PrzeliczenieRoznicyDni(DostawaRow dostawa)
        {
            if (isLoading || dpDataWstawienia.SelectedDate == null || dostawa.DpData == null) return;
            if (dostawa.DpData.SelectedDate != null)
            {
                int roznica = (dostawa.DpData.SelectedDate.Value - dpDataWstawienia.SelectedDate.Value).Days;
                if (dostawa.TxtDni != null) dostawa.TxtDni.Text = roznica.ToString();
                // Sync TxtDoba (chroni przed niespójnością gdy save kiedyś znów spróbuje czytać Doba)
                if (dostawa.TxtDoba != null && dostawa.TxtDoba.Text != roznica.ToString())
                {
                    isLoading = true;
                    try { dostawa.TxtDoba.Text = roznica.ToString(); }
                    finally { isLoading = false; }
                }
            }
        }

        private void ObliczAutaWyliczone(DostawaRow dostawa)
        {
            if (isLoading || dostawa.TxtSztuki == null || dostawa.TxtSztPoj == null || dostawa.TxtAutaWyliczone == null) return;

            if (double.TryParse(dostawa.TxtSztuki.Text, out double sztuki) &&
                double.TryParse(dostawa.TxtSztPoj.Text, out double sztPoj) && sztPoj > 0)
            {
                double auta = sztuki / (sztPoj * 264);
                dostawa.TxtAutaWyliczone.Text = auta.ToString("F2");
            }
        }

        private void PrzeliczenieZMnoznika(DostawaRow dostawa)
        {
            if (isLoading || dostawa.TxtMnoznik == null || dostawa.TxtSztPoj == null || dostawa.TxtSztuki == null) return;
            if (double.TryParse(dostawa.TxtMnoznik.Text, out double mnoznik) && double.TryParse(dostawa.TxtSztPoj.Text, out double sztPoj))
            {
                double wyliczone = mnoznik * sztPoj * 264;
                dostawa.TxtSztuki.Text = wyliczone.ToString("0");

                if (dostawa.TxtAutaReczne != null)
                {
                    dostawa.TxtAutaReczne.Text = mnoznik.ToString("0");
                }

                ObliczAutaWyliczone(dostawa);
            }
        }

        private void WklejRoznice(DostawaRow dostawa)
        {
            if (dostawa.TxtSztuki != null)
            {
                dostawa.TxtSztuki.Text = txtSztukiRoznica.Text;

                if (dostawa.TxtAutaWyliczone != null && dostawa.TxtAutaReczne != null)
                {
                    if (double.TryParse(dostawa.TxtAutaWyliczone.Text, out double autaWyl))
                    {
                        int zaokraglone = (int)Math.Ceiling(autaWyl);
                        dostawa.TxtAutaReczne.Text = zaokraglone.ToString();
                    }
                }

                ObliczAutaWyliczone(dostawa);
            }
        }

        #endregion

        #region Zapis

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (!Waliduj()) return;

            if (!Modyfikacja && dpDataWstawienia.SelectedDate.HasValue && cmbDostawca.SelectedItem != null)
            {
                bool anuluj = SprawdzPodobneWstawienie(dpDataWstawienia.SelectedDate.Value, cmbDostawca.SelectedItem.ToString());
                if (anuluj) return;
            }

            var (typUmowy, typCeny, bufor) = JakiTypKontraktu();
            if (string.IsNullOrEmpty(typUmowy)) return;

            bool zapiszAuta = (typUmowy != "Wolnyrynek");
            bool maNotatki = !string.IsNullOrWhiteSpace(txtNotatki?.Text);
            int iloscNotatek = 0;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlTransaction trans = conn.BeginTransaction())
                    {
                        try
                        {
                            if (trybSerii)
                            {
                                int zapisane = 0;

                                // NAJPIERW zapisz wstawienie #1 z panelJednePodstawy
                                if (dpDataWstawienia.SelectedDate != null && !string.IsNullOrWhiteSpace(txtSztukiWstawienia.Text))
                                {
                                    if (int.TryParse(txtSztukiWstawienia.Text, out int iloscPierwszego))
                                    {
                                        long lpW = NowyNumerWstawienia(conn, trans);
                                        WstawWstawienieDb(conn, trans, lpW, dpDataWstawienia.SelectedDate.Value, iloscPierwszego, typUmowy, typCeny);

                                        foreach (var dostawa in dostawyRows)
                                        {
                                            WstawDostaweDb(conn, trans, dostawa, lpW, dpDataWstawienia.SelectedDate.Value, typUmowy, typCeny, bufor, zapiszAuta);
                                            if (maNotatki) iloscNotatek++;
                                        }
                                        zapisane++;
                                    }
                                }

                                // POTEM zapisz pozostałe wstawienia z serii
                                foreach (var seria in seriaRows)
                                {
                                    if (seria.DpData?.SelectedDate == null || string.IsNullOrWhiteSpace(seria.TxtIlosc?.Text))
                                        continue;

                                    if (!int.TryParse(seria.TxtIlosc.Text, out int ilosc))
                                        continue;

                                    long lpW = NowyNumerWstawienia(conn, trans);
                                    WstawWstawienieDb(conn, trans, lpW, seria.DpData.SelectedDate.Value, ilosc, typUmowy, typCeny);

                                    foreach (var dostawa in dostawyRows)
                                    {
                                        WstawDostaweDb(conn, trans, dostawa, lpW, seria.DpData.SelectedDate.Value, typUmowy, typCeny, bufor, zapiszAuta);
                                        if (maNotatki) iloscNotatek++;
                                    }
                                    zapisane++;
                                }
                                trans.Commit();

                                _zapisanoSukces = true;
                                _zapiszInfoAuta = zapiszAuta;
                                _zapiszIloscNotatek = iloscNotatek;
                            }
                            else
                            {
                                long lpW;
                                if (Modyfikacja)
                                {
                                    lpW = LpWstawienia;

                                    if (!int.TryParse(txtSztukiWstawienia.Text, out int iloscWst))
                                    {
                                        MessageBox.Show("Nieprawidłowa ilość sztuk!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                                        return;
                                    }

                                    AktualizujWstawienie(conn, trans, lpW, typUmowy, typCeny, iloscWst);
                                    // SynchronizujDostawy zachowuje istniejące Lp (UPDATE+INSERT+DELETE diff)
                                    // zamiast DELETE+INSERT, co rwało relacje z PartiaDostawca/QC.
                                    SynchronizujDostawy(conn, trans, lpW,
                                        dpDataWstawienia.SelectedDate.Value, typUmowy, typCeny, bufor, zapiszAuta);
                                }
                                else
                                {
                                    lpW = NowyNumerWstawienia(conn, trans);

                                    if (!int.TryParse(txtSztukiWstawienia.Text, out int iloscWst))
                                    {
                                        MessageBox.Show("Nieprawidłowa ilość sztuk!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                                        return;
                                    }

                                    WstawWstawienieDb(conn, trans, lpW, dpDataWstawienia.SelectedDate.Value, iloscWst, typUmowy, typCeny);

                                    foreach (var dostawa in dostawyRows)
                                    {
                                        WstawDostaweDb(conn, trans, dostawa, lpW, dpDataWstawienia.SelectedDate.Value, typUmowy, typCeny, bufor, zapiszAuta);
                                        if (maNotatki) iloscNotatek++;
                                    }
                                }
                                trans.Commit();

                                // Komunikat o sukcesie zostanie pokazany jako toast po zamknięciu okna.
                                _zapisanoSukces = true;
                                _zapiszInfoAuta = zapiszAuta;
                                _zapiszIloscNotatek = iloscNotatek;
                            }

                            DialogResult = true;
                            Close();
                        }
                        catch (Exception ex)
                        {
                            trans.Rollback();
                            throw new Exception($"Błąd podczas zapisu: {ex.Message}", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}\n\nSzczegóły: {ex.StackTrace}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool Waliduj()
        {
            if (cmbDostawca.SelectedItem == null)
            {
                MessageBox.Show("Wybierz dostawcę", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (trybSerii)
            {
                if (seriaRows.Count == 0)
                {
                    MessageBox.Show("Dodaj przynajmniej jedno wstawienie do serii", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            else
            {
                if (dpDataWstawienia.SelectedDate == null || string.IsNullOrWhiteSpace(txtSztukiWstawienia.Text))
                {
                    MessageBox.Show("Wypełnij datę i ilość", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            if (dostawyRows.Count == 0)
            {
                MessageBox.Show("Dodaj przynajmniej jedną dostawę", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            // Walidacja każdego wiersza dostawy: data + sztuki muszą być wypełnione
            DateTime? dataWst = trybSerii ? null : dpDataWstawienia.SelectedDate;
            for (int i = 0; i < dostawyRows.Count; i++)
            {
                var d = dostawyRows[i];
                int nr = i + 1;

                if (d.DpData?.SelectedDate == null)
                {
                    MessageBox.Show($"Dostawa #{nr}: brak daty odbioru.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                    d.DpData?.Focus();
                    return false;
                }

                if (string.IsNullOrWhiteSpace(d.TxtSztuki?.Text)
                    || !int.TryParse(d.TxtSztuki.Text, out int sztDostawa)
                    || sztDostawa <= 0)
                {
                    MessageBox.Show($"Dostawa #{nr}: nieprawidłowa ilość sztuk.", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                    d.TxtSztuki?.Focus();
                    return false;
                }

                // Data odbioru nie może być wcześniejsza niż data wstawienia
                if (dataWst.HasValue && d.DpData.SelectedDate.Value.Date < dataWst.Value.Date)
                {
                    MessageBox.Show(
                        $"Dostawa #{nr}: data odbioru ({d.DpData.SelectedDate.Value:yyyy-MM-dd}) " +
                        $"jest wcześniejsza niż data wstawienia ({dataWst.Value:yyyy-MM-dd}).",
                        "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                    d.DpData.Focus();
                    return false;
                }
            }

            return true;
        }

        // Mapowanie TypCeny → kolor + emoji (używane w przyciskach i etykietach)
        private static (Color bg, Color fg, string emoji, string opis) StylTypuCeny(string typCeny)
        {
            switch ((typCeny ?? "").ToLowerInvariant())
            {
                case "łączona":     return (Color.FromRgb(138, 43, 226), Colors.White, "🔗", "łączona");
                case "rolnicza":    return (Color.FromRgb(92, 138, 58),  Colors.White, "🌾", "rolnicza");
                case "wolnyrynek":  return (Color.FromRgb(255, 193, 7),  Colors.Black, "💰", "wolnyrynek");
                case "ministerialna": return (Color.FromRgb(33, 150, 243), Colors.White, "🏛️", "ministerialna");
                default:            return (Color.FromRgb(120, 120, 120), Colors.White, "❓", typCeny ?? "—");
            }
        }

        // bufor zależy od kombinacji TypUmowy + TypCeny
        private static string BuforDla(string typUmowy)
        {
            switch (typUmowy)
            {
                case "W.Wolnyrynek": return "B.Wolny.";
                case "Wolnyrynek":   return "Do wykupienia";
                case "Kontrakt":     return "B.Kontr.";
                default:             return "";
            }
        }

        // Etykieta typu umowy z ikoną
        private static string OpisTypuUmowy(string typUmowy)
        {
            switch (typUmowy)
            {
                case "W.Wolnyrynek": return "Wolny rynek (wierny) ⭐";
                case "Wolnyrynek":   return "Wolny rynek 💰";
                case "Kontrakt":     return "Kontrakt 📝";
                default:             return typUmowy ?? "—";
            }
        }

        private (string, string, string) JakiTypKontraktu()
        {
            // Tryb modyfikacji + zachowane wartości → najpierw pytanie czy zmienić.
            if (Modyfikacja && !string.IsNullOrEmpty(BiezacyTypUmowy) && !string.IsNullOrEmpty(BiezacyTypCeny))
            {
                var dec = PokazPodgladTypuCeny(BiezacyTypUmowy, BiezacyTypCeny);
                if (dec == PodgladDecyzja.Anuluj) return (null, null, null);
                if (dec == PodgladDecyzja.Zachowaj)
                    return (BiezacyTypUmowy, BiezacyTypCeny, BuforDla(BiezacyTypUmowy));
                // Zmiana → spadamy niżej do wyboru
            }

            // Krok 1: jaki hodowca
            var hodowca = WybierzTypHodowcy();
            if (hodowca == null) return (null, null, null);

            if (hodowca == "Wolnyrynek")
            {
                // Krok 2a: wierny czy nie
                bool? wierny = CzyWierny();
                if (wierny == null) return (null, null, null);
                return wierny.Value
                    ? ("W.Wolnyrynek", "wolnyrynek", "B.Wolny.")
                    : ("Wolnyrynek", "wolnyrynek", "Do wykupienia");
            }
            else // "Kontrakt"
            {
                // Krok 2b: typ ceny kontraktowej
                string typCeny = WybierzTypCeny();
                return !string.IsNullOrEmpty(typCeny)
                    ? ("Kontrakt", typCeny, "B.Kontr.")
                    : (null, null, null);
            }
        }

        private enum PodgladDecyzja { Zachowaj, Zmien, Anuluj }

        // Podgląd aktualnego typu z kolorem + pytanie zachować/zmienić
        private PodgladDecyzja PokazPodgladTypuCeny(string typUmowy, string typCeny)
        {
            var (bg, fg, emoji, opis) = StylTypuCeny(typCeny);

            var dialog = new Window
            {
                Title = "Bieżący typ ceny",
                Width = 460,
                Height = 360,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(245, 247, 250))
            };

            var root = new StackPanel { Margin = new Thickness(20) };

            root.Children.Add(new TextBlock
            {
                Text = "💼 Bieżący typ ceny wstawienia",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12),
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            });

            // Karta z aktualnym typem (kolor!)
            var card = new Border
            {
                Background = new SolidColorBrush(bg),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var cardStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            cardStack.Children.Add(new TextBlock
            {
                Text = emoji,
                FontSize = 38,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            });
            cardStack.Children.Add(new TextBlock
            {
                Text = opis.ToUpper(),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(fg),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            cardStack.Children.Add(new TextBlock
            {
                Text = OpisTypuUmowy(typUmowy),
                FontSize = 13,
                Foreground = new SolidColorBrush(fg),
                Opacity = 0.9,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            });
            card.Child = cardStack;
            root.Children.Add(card);

            root.Children.Add(new TextBlock
            {
                Text = "Czy chcesz zmienić typ ceny?",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(80, 80, 80)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var btnRow = new Grid();
            btnRow.ColumnDefinitions.Add(new ColumnDefinition());
            btnRow.ColumnDefinitions.Add(new ColumnDefinition());

            var decyzja = PodgladDecyzja.Anuluj;

            var btnZachowaj = new Button
            {
                Content = "🔒 Zachowaj",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 6, 0),
                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnZachowaj.Click += (s, e) => { decyzja = PodgladDecyzja.Zachowaj; dialog.DialogResult = true; };
            Grid.SetColumn(btnZachowaj, 0);
            btnRow.Children.Add(btnZachowaj);

            var btnZmien = new Button
            {
                Content = "✏️ Zmień",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(12),
                Margin = new Thickness(6, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(243, 156, 18)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnZmien.Click += (s, e) => { decyzja = PodgladDecyzja.Zmien; dialog.DialogResult = true; };
            Grid.SetColumn(btnZmien, 1);
            btnRow.Children.Add(btnZmien);

            root.Children.Add(btnRow);
            dialog.Content = root;
            dialog.ShowDialog();
            return decyzja;
        }

        // Wybór typu hodowcy: Wolnyrynek vs Kontrakt
        private string WybierzTypHodowcy()
        {
            var dialog = new Window
            {
                Title = "Jaki hodowca?",
                Width = 480,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(245, 247, 250))
            };

            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock
            {
                Text = "🐔 Jaki hodowca?",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            });

            var btnRow = new Grid();
            btnRow.ColumnDefinitions.Add(new ColumnDefinition());
            btnRow.ColumnDefinitions.Add(new ColumnDefinition());

            string wybor = null;

            var btnWolny = StworzDuzyPrzycisk("💰", "Wolnyrynek", Color.FromRgb(255, 193, 7), Colors.Black);
            btnWolny.Margin = new Thickness(0, 0, 6, 0);
            btnWolny.Click += (s, e) => { wybor = "Wolnyrynek"; dialog.DialogResult = true; };
            Grid.SetColumn(btnWolny, 0);
            btnRow.Children.Add(btnWolny);

            var btnKontr = StworzDuzyPrzycisk("📝", "Kontraktowy", Color.FromRgb(33, 150, 243), Colors.White);
            btnKontr.Margin = new Thickness(6, 0, 0, 0);
            btnKontr.Click += (s, e) => { wybor = "Kontrakt"; dialog.DialogResult = true; };
            Grid.SetColumn(btnKontr, 1);
            btnRow.Children.Add(btnKontr);

            root.Children.Add(btnRow);
            dialog.Content = root;
            dialog.ShowDialog();
            return wybor;
        }

        // Wybór: wierny czy nie (dla Wolnyrynek)
        private bool? CzyWierny()
        {
            var dialog = new Window
            {
                Title = "Wierny hodowca?",
                Width = 480,
                Height = 320,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(245, 247, 250))
            };

            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock
            {
                Text = "⭐ Czy hodowca jest wierny?",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            });

            var btnRow = new Grid();
            btnRow.ColumnDefinitions.Add(new ColumnDefinition());
            btnRow.ColumnDefinitions.Add(new ColumnDefinition());

            bool? wybor = null;

            var btnTak = StworzDuzyPrzycisk("⭐", "Tak — wierny", Color.FromRgb(46, 204, 113), Colors.White);
            btnTak.Margin = new Thickness(0, 0, 6, 0);
            btnTak.Click += (s, e) => { wybor = true; dialog.DialogResult = true; };
            Grid.SetColumn(btnTak, 0);
            btnRow.Children.Add(btnTak);

            var btnNie = StworzDuzyPrzycisk("🆕", "Nie", Color.FromRgb(149, 165, 166), Colors.White);
            btnNie.Margin = new Thickness(6, 0, 0, 0);
            btnNie.Click += (s, e) => { wybor = false; dialog.DialogResult = true; };
            Grid.SetColumn(btnNie, 1);
            btnRow.Children.Add(btnNie);

            root.Children.Add(btnRow);
            dialog.Content = root;
            dialog.ShowDialog();
            return wybor;
        }

        private Button StworzDuzyPrzycisk(string emoji, string label, Color bg, Color fg)
        {
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock
            {
                Text = emoji,
                FontSize = 42,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(fg),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            return new Button
            {
                Content = stack,
                Background = new SolidColorBrush(bg),
                Foreground = new SolidColorBrush(fg),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Padding = new Thickness(8),
                MinHeight = 180
            };
        }

        private string WybierzTypCeny()
        {
            var dialog = new Window
            {
                Title = "Wybierz typ ceny kontraktowej",
                Width = 540,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(245, 247, 250))
            };

            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock
            {
                Text = "📝 Wybierz typ ceny kontraktowej",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 16),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(44, 62, 80))
            });

            string wybrana = null;

            var opcje = new[]
            {
                ("łączona",      "🔗", "Łączona — kombinacja stawek", Color.FromRgb(138, 43, 226), Colors.White),
                ("rolnicza",     "🌾", "Rolnicza — według GUS",        Color.FromRgb(92, 138, 58),  Colors.White),
                ("wolnyrynek",   "💰", "Wolnyrynek — cena rynkowa",    Color.FromRgb(255, 193, 7),  Colors.Black),
                ("ministerialna","🏛️", "Ministerialna — stawka MRiRW", Color.FromRgb(33, 150, 243), Colors.White),
            };

            foreach (var (key, emoji, opis, bg, fg) in opcje)
            {
                var content = new Grid { Margin = new Thickness(8) };
                content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(64) });
                content.ColumnDefinitions.Add(new ColumnDefinition());

                content.Children.Add(new TextBlock
                {
                    Text = emoji,
                    FontSize = 32,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                labels.Children.Add(new TextBlock
                {
                    Text = key,
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(fg)
                });
                labels.Children.Add(new TextBlock
                {
                    Text = opis,
                    FontSize = 12,
                    Opacity = 0.85,
                    Foreground = new SolidColorBrush(fg),
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(labels, 1);
                content.Children.Add(labels);

                var btn = new Button
                {
                    Content = content,
                    Background = new SolidColorBrush(bg),
                    Foreground = new SolidColorBrush(fg),
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(0),
                    Cursor = Cursors.Hand,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch
                };
                btn.Click += (s, e) => { wybrana = key; dialog.DialogResult = true; };
                root.Children.Add(btn);
            }

            dialog.Content = root;
            dialog.ShowDialog();
            return wybrana;
        }

        private long NowyNumerWstawienia(SqlConnection conn, SqlTransaction trans)
        {
            // UPDLOCK + HOLDLOCK aby równoległe transakcje nie dostały tego samego Lp
            using (var cmd = new SqlCommand("SELECT MAX(Lp) FROM dbo.WstawieniaKurczakow WITH (UPDLOCK, HOLDLOCK)", conn, trans))
            {
                object v = cmd.ExecuteScalar();
                return (v == DBNull.Value) ? 1 : Convert.ToInt64(v) + 1;
            }
        }

        private void WstawWstawienieDb(SqlConnection conn, SqlTransaction trans, long lpW, DateTime data, int ilosc, string typUmowy, string typCeny)
        {
            const string sql = @"INSERT INTO dbo.WstawieniaKurczakow (Lp, Dostawca, DataWstawienia, IloscWstawienia, DataUtw, KtoStwo, Uwagi, TypUmowy, TypCeny)
                VALUES (@Lp, @D, @DW, @Il, SYSDATETIME(), @Kto, @Uw, @TU, @TC)";
            using (var cmd = new SqlCommand(sql, conn, trans))
            {
                cmd.Parameters.AddWithValue("@Lp", lpW);
                cmd.Parameters.AddWithValue("@D", cmbDostawca.SelectedItem.ToString());
                cmd.Parameters.AddWithValue("@DW", data);
                cmd.Parameters.AddWithValue("@Il", ilosc);
                cmd.Parameters.AddWithValue("@Kto", UserID ?? "");
                cmd.Parameters.AddWithValue("@Uw", txtNotatki?.Text ?? "");
                cmd.Parameters.AddWithValue("@TU", typUmowy);
                cmd.Parameters.AddWithValue("@TC", typCeny);
                cmd.ExecuteNonQuery();
            }
        }

        private void ChkPomoc_Changed(object sender, RoutedEventArgs e)
        {
            if (kolumnaPomoc != null)
            {
                if (chkPomoc.IsChecked == true)
                {
                    // Rozszerz okno i pokaż kolumnę pomocy
                    this.Width = 1300;
                    kolumnaPomoc.Width = new GridLength(300);
                }
                else
                {
                    // Zmniejsz okno i ukryj kolumnę pomocy
                    this.Width = 980;
                    kolumnaPomoc.Width = new GridLength(0);
                }
            }
        }

        private void ChkDostawy_Changed(object sender, RoutedEventArgs e)
        {
            if (panelDostawy != null)
            {
                panelDostawy.Visibility = chkDostawy.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        #region Kalendarz Dostaw

        private void ChkKalendarz_Changed(object sender, RoutedEventArgs e)
        {
            if (kolumnaKalendarz != null)
            {
                if (chkKalendarz.IsChecked == true)
                {
                    // Rozszerz okno i pokaż kolumnę kalendarza (większa szerokość dla szczegółów dostaw)
                    this.Width = Math.Max(this.Width, 1400);
                    kolumnaKalendarz.Width = new GridLength(420);
                    LoadDeliveryDatesForCalendar();
                    RenderMiniCalendar();
                }
                else
                {
                    // Ukryj kolumnę kalendarza
                    kolumnaKalendarz.Width = new GridLength(0);
                    if (chkPomoc.IsChecked != true)
                    {
                        this.Width = 980;
                    }
                }
            }
        }

        private void BtnCalendarPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentWeekIndex > 0)
            {
                _currentWeekIndex--;
                RenderWeeksList();
            }
        }

        private void BtnCalendarNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentWeekIndex + 1 < _weeksWithDeliveries.Count)
            {
                _currentWeekIndex++;
                RenderWeeksList();
            }
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = ((int)date.DayOfWeek + 6) % 7;
            return date.AddDays(-diff).Date;
        }

        private int GetWeekOfYear(DateTime date)
        {
            return _plCulture.Calendar.GetWeekOfYear(date, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
        }

        private void LoadDeliveryDatesForCalendar()
        {
            _deliveryDates.Clear();
            _originalDeliveryDates.Clear();
            _weeksWithDeliveries.Clear();

            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();

                // Załaduj wszystkie dostawy z bazy ze szczegółami (6 miesięcy wstecz i 6 miesięcy wprzód)
                const string query = @"
                    SELECT
                        DataOdbioru,
                        Dostawca,
                        ISNULL(Auta, 0) as Auta,
                        ISNULL(SztukiDek, 0) as Sztuki,
                        ISNULL(WagaDek, 0) as Waga,
                        ISNULL(Bufor, 'Planowany') as Status
                    FROM [LibraNet].[dbo].[HarmonogramDostaw]
                    WHERE DataOdbioru IS NOT NULL
                      AND DataOdbioru >= DATEADD(MONTH, -6, GETDATE())
                      AND DataOdbioru <= DATEADD(MONTH, 6, GETDATE())
                      AND (Bufor IS NULL OR Bufor NOT IN ('Anulowany', 'Sprzedany'))
                    ORDER BY DataOdbioru, Dostawca";

                using var cmd = new SqlCommand(query, conn);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                    {
                        var date = reader.GetDateTime(0).Date;
                        var item = new MiniDeliveryItem
                        {
                            Dostawca = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Auta = Convert.ToInt32(reader.GetValue(2)),
                            Sztuki = Convert.ToInt32(reader.GetValue(3)),
                            Waga = Convert.ToDecimal(reader.GetValue(4)),
                            Status = reader.IsDBNull(5) ? "Planowany" : reader.GetString(5)
                        };

                        if (!_deliveryDates.ContainsKey(date))
                            _deliveryDates[date] = new List<MiniDeliveryItem>();
                        _deliveryDates[date].Add(item);

                        if (!_originalDeliveryDates.ContainsKey(date))
                            _originalDeliveryDates[date] = new List<MiniDeliveryItem>();
                        _originalDeliveryDates[date].Add(item);
                    }
                }

                // Znajdź tygodnie z dostawami
                FindWeeksWithDeliveries();

                // Ustaw bieżący tydzień
                ScrollToCurrentWeek();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania dat dostaw: {ex.Message}");
            }
        }

        private void FindWeeksWithDeliveries()
        {
            _weeksWithDeliveries.Clear();

            if (!_deliveryDates.Any()) return;

            var minDate = _deliveryDates.Keys.Min();
            var maxDate = _deliveryDates.Keys.Max();

            var weekStart = GetStartOfWeek(minDate);
            while (weekStart <= maxDate)
            {
                var weekEnd = weekStart.AddDays(6);
                var weekDeliveries = _deliveryDates
                    .Where(kvp => kvp.Key >= weekStart && kvp.Key <= weekEnd)
                    .ToList();

                if (weekDeliveries.Any())
                {
                    _weeksWithDeliveries.Add(new MiniWeekData
                    {
                        WeekStart = weekStart,
                        WeekEnd = weekEnd,
                        WeekNumber = GetWeekOfYear(weekStart),
                        DayDeliveries = weekDeliveries.OrderBy(kvp => kvp.Key).ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        TotalSztuki = weekDeliveries.Sum(kvp => kvp.Value.Sum(d => d.Sztuki))
                    });
                }

                weekStart = weekStart.AddDays(7);
            }
        }

        private void ScrollToCurrentWeek()
        {
            var currentWeekStart = GetStartOfWeek(DateTime.Today);
            _currentWeekIndex = _weeksWithDeliveries.FindIndex(w => w.WeekStart >= currentWeekStart);
            if (_currentWeekIndex < 0) _currentWeekIndex = Math.Max(0, _weeksWithDeliveries.Count - 1);
        }

        private void RenderMiniCalendar()
        {
            RenderWeeksList();
        }

        private void RenderWeeksList()
        {
            if (panelWeeksList == null) return;

            panelWeeksList.Children.Clear();

            // Zbierz planowane daty z formularza
            var plannedDates = new HashSet<DateTime>();
            var plannedSztByDate = new Dictionary<DateTime, int>();
            foreach (var row in dostawyRows)
            {
                if (row.DpData?.SelectedDate != null)
                {
                    var date = row.DpData.SelectedDate.Value.Date;
                    plannedDates.Add(date);
                    int szt = 0;
                    if (int.TryParse(row.TxtSztuki?.Text?.Replace(" ", ""), out szt) && szt > 0)
                    {
                        if (!plannedSztByDate.ContainsKey(date))
                            plannedSztByDate[date] = 0;
                        plannedSztByDate[date] += szt;
                    }
                }
            }

            if (!_weeksWithDeliveries.Any())
            {
                txtCalendarMonth.Text = "Brak dostaw";
                txtCalendarDeliveryCount.Text = "Tygodni: 0";
                txtCalendarTotalSzt.Text = "";
                return;
            }

            // Ensure index is valid
            _currentWeekIndex = Math.Max(0, Math.Min(_currentWeekIndex, _weeksWithDeliveries.Count - 1));

            // Get current week to display
            var week = _weeksWithDeliveries[_currentWeekIndex];

            // Update header
            txtCalendarMonth.Text = $"tyg.{week.WeekNumber} ({week.WeekStart:dd.MM}-{week.WeekEnd:dd.MM})";
            txtCalendarDeliveryCount.Text = $"Tydzień {_currentWeekIndex + 1} z {_weeksWithDeliveries.Count}";
            txtCalendarTotalSzt.Text = $"Łącznie: {week.TotalSztuki:# ##0} szt.";

            // Render week header
            var weekHeader = CreateWeekHeader(week);
            panelWeeksList.Children.Add(weekHeader);

            // Add column headers
            var columnHeaders = CreateColumnHeaders();
            panelWeeksList.Children.Add(columnHeaders);

            // Render days with deliveries
            foreach (var dayEntry in week.DayDeliveries.OrderBy(d => d.Key))
            {
                bool isPlanned = plannedDates.Contains(dayEntry.Key);
                int plannedSzt = plannedSztByDate.GetValueOrDefault(dayEntry.Key, 0);

                // Create day header
                var dayHeader = CreateDayHeader(dayEntry.Key, dayEntry.Value, isPlanned, plannedSzt);
                panelWeeksList.Children.Add(dayHeader);

                // Create delivery rows under the day header
                foreach (var delivery in dayEntry.Value.OrderByDescending(d => d.Sztuki))
                {
                    var deliveryRow = CreateDeliveryRow(delivery);
                    panelWeeksList.Children.Add(deliveryRow);
                }
            }

            // Add week summary row at the end
            var weekSummary = CreateWeekSummaryRow(week);
            panelWeeksList.Children.Add(weekSummary);
        }

        private Border CreateWeekHeader(MiniWeekData week)
        {
            var isCurrentWeek = week.WeekStart <= DateTime.Today && week.WeekEnd >= DateTime.Today;
            var bgColor = isCurrentWeek ? Color.FromRgb(59, 130, 246) : Color.FromRgb(92, 138, 58);
            var glowColor = isCurrentWeek ? Color.FromRgb(33, 150, 243) : Color.FromRgb(76, 175, 80);

            var header = new Border
            {
                Background = new SolidColorBrush(bgColor),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 2),
                CornerRadius = new CornerRadius(4)
            };

            // Add glow effect
            var glowEffect = new DropShadowEffect
            {
                Color = glowColor,
                BlurRadius = 10,
                ShadowDepth = 0,
                Opacity = isCurrentWeek ? 0.7 : 0.4
            };
            header.Effect = glowEffect;

            // Apply pulsing animation for current week
            if (isCurrentWeek)
            {
                var pulseOpacity = new DoubleAnimation
                {
                    From = 0.5,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(800),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                var pulseBlur = new DoubleAnimation
                {
                    From = 8,
                    To = 18,
                    Duration = TimeSpan.FromMilliseconds(800),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, pulseOpacity);
                glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, pulseBlur);
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var weekText = new TextBlock
            {
                Text = $"tyg.{week.WeekNumber} ({week.WeekStart:dd.MM}-{week.WeekEnd:dd.MM})",
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(weekText, 0);
            grid.Children.Add(weekText);

            // Capacity percentage
            var capacityPercent = (double)week.TotalSztuki / (MAX_DAILY_CAPACITY * 7) * 100;
            var totalText = new TextBlock
            {
                Text = $"{week.TotalSztuki:# ##0} szt. {capacityPercent:0}%",
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(totalText, 1);
            grid.Children.Add(totalText);

            header.Child = grid;
            return header;
        }

        private Border CreateWeekSummaryRow(MiniWeekData week)
        {
            var sumRow = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(52, 73, 94)), // Dark blue-gray
                Padding = new Thickness(4, 4, 4, 4),
                Margin = new Thickness(0, 4, 0, 0),
                CornerRadius = new CornerRadius(4)
            };

            // Add pulsing orange glow
            var glowEffect = new DropShadowEffect
            {
                Color = Color.FromRgb(255, 167, 38), // Orange
                BlurRadius = 12,
                ShadowDepth = 0,
                Opacity = 0.7
            };
            sumRow.Effect = glowEffect;

            // Pulsing animation for sum row
            var pulseOpacity = new DoubleAnimation
            {
                From = 0.5,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(800),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            var pulseBlur = new DoubleAnimation
            {
                From = 8,
                To = 20,
                Duration = TimeSpan.FromMilliseconds(800),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, pulseOpacity);
            glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, pulseBlur);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Label
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) }); // Suma szt
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Auta

            // Sum label
            var labelText = new TextBlock
            {
                Text = $"★ SUMA tyg.{week.WeekNumber}",
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelText, 0);
            grid.Children.Add(labelText);

            // Total pieces
            var sztukiText = new TextBlock
            {
                Text = $"{week.TotalSztuki:# ##0} szt.",
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7)), // Yellow/gold
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sztukiText, 1);
            grid.Children.Add(sztukiText);

            // Total auta
            var totalAuta = week.DayDeliveries.Values.SelectMany(d => d).Sum(d => d.Auta);
            var autaText = new TextBlock
            {
                Text = $"{totalAuta} aut",
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(autaText, 2);
            grid.Children.Add(autaText);

            sumRow.Child = grid;
            return sumRow;
        }

        private Border CreateColumnHeaders()
        {
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(243, 244, 246)),
                Padding = new Thickness(4, 2, 4, 2),
                BorderBrush = new SolidColorBrush(Color.FromRgb(209, 213, 219)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Hodowca
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) }); // Auto
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) }); // Szt
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // Waga
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) }); // Status

            var headerStyle = new Style(typeof(TextBlock));

            var hodowcaHeader = new TextBlock { Text = "Hodowca", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99)) };
            Grid.SetColumn(hodowcaHeader, 0);
            grid.Children.Add(hodowcaHeader);

            var autaHeader = new TextBlock { Text = "Aut", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99)), HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetColumn(autaHeader, 1);
            grid.Children.Add(autaHeader);

            var sztukiHeader = new TextBlock { Text = "Szt", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99)), HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(sztukiHeader, 2);
            grid.Children.Add(sztukiHeader);

            var wagaHeader = new TextBlock { Text = "Waga", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99)), HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(wagaHeader, 3);
            grid.Children.Add(wagaHeader);

            var statusHeader = new TextBlock { Text = "Status", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(75, 85, 99)), HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetColumn(statusHeader, 4);
            grid.Children.Add(statusHeader);

            header.Child = grid;
            return header;
        }

        private Border CreateDayHeader(DateTime date, List<MiniDeliveryItem> deliveries, bool isPlanned = false, int plannedSzt = 0)
        {
            var totalSzt = deliveries.Sum(d => d.Sztuki);
            var capacityPercent = (double)totalSzt / MAX_DAILY_CAPACITY * 100;
            var isToday = date.Date == DateTime.Today;

            // Determine background color and glow color based on capacity
            Color bgColor, glowColor;
            bool shouldPulse = false;

            if (capacityPercent >= 100)
            {
                bgColor = Color.FromRgb(183, 28, 28); // Dark red
                glowColor = Color.FromRgb(244, 67, 54); // Red glow
                shouldPulse = true;
            }
            else if (capacityPercent >= 80)
            {
                bgColor = Color.FromRgb(239, 68, 68); // Red
                glowColor = Color.FromRgb(239, 68, 68);
            }
            else if (capacityPercent >= 50)
            {
                bgColor = Color.FromRgb(251, 191, 36); // Yellow/Orange
                glowColor = Color.FromRgb(255, 193, 7);
            }
            else
            {
                bgColor = Color.FromRgb(34, 197, 94); // Green
                glowColor = Color.FromRgb(76, 175, 80);
            }

            if (isPlanned)
            {
                bgColor = Color.FromRgb(147, 51, 234); // Purple for planned
                glowColor = Color.FromRgb(156, 39, 176);
                shouldPulse = true;
            }

            if (isToday && !isPlanned)
            {
                bgColor = Color.FromRgb(59, 130, 246); // Blue for today
                glowColor = Color.FromRgb(33, 150, 243);
                shouldPulse = true; // Today always pulses
            }

            var dayName = date.ToString("ddd", _plCulture).ToLower().TrimEnd('.');

            var header = new Border
            {
                Background = new SolidColorBrush(bgColor),
                Padding = new Thickness(4, 3, 4, 3),
                Margin = new Thickness(0, 3, 0, 0),
                CornerRadius = new CornerRadius(3)
            };

            // Add glow effect
            var glowEffect = new DropShadowEffect
            {
                Color = glowColor,
                BlurRadius = shouldPulse ? 12 : 6,
                ShadowDepth = 0,
                Opacity = shouldPulse ? 0.7 : 0.4
            };
            header.Effect = glowEffect;

            // Apply pulsing animation
            if (shouldPulse)
            {
                var pulseOpacity = new DoubleAnimation
                {
                    From = 0.4,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(700),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                var pulseBlur = new DoubleAnimation
                {
                    From = 8,
                    To = 22,
                    Duration = TimeSpan.FromMilliseconds(700),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, pulseOpacity);
                glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, pulseBlur);
            }

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Day
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Sztuki + %

            // Day label with planned indicator and star for high capacity
            var starPrefix = (capacityPercent >= 100 || isPlanned) ? "★ " : "";
            var dayLabel = $"{starPrefix}{dayName}.{date:dd.MM}";
            var dayText = new TextBlock
            {
                Text = dayLabel,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(dayText, 0);
            grid.Children.Add(dayText);

            // Sztuki + percent with highlighted badge
            var sumaBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(4, 1, 4, 1),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var sztukiDisplay = isPlanned && plannedSzt > 0
                ? $"{totalSzt:# ##0} (+{plannedSzt:# ##0}) {capacityPercent:0}%"
                : $"{totalSzt:# ##0} szt. {capacityPercent:0}%";
            var sztukiText = new TextBlock
            {
                Text = sztukiDisplay,
                FontWeight = FontWeights.Bold,
                FontSize = 10,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            sumaBorder.Child = sztukiText;
            Grid.SetColumn(sumaBorder, 1);
            grid.Children.Add(sumaBorder);

            header.Child = grid;
            return header;
        }

        private Border CreateDeliveryRow(MiniDeliveryItem delivery)
        {
            var isConfirmed = delivery.Status == "Potwierdzony";
            var isPartiallyConfirmed = delivery.Status == "B.Kontr." || delivery.Status == "B.Wolny";
            var isPlanned = string.IsNullOrEmpty(delivery.Status) || delivery.Status == "Planowany" || delivery.Status == "Do w.";

            // Background and glow colors based on status
            Color bgColor, glowColor;
            bool shouldPulse = false;

            if (isConfirmed)
            {
                bgColor = Color.FromRgb(200, 230, 201); // Light green
                glowColor = Color.FromRgb(76, 175, 80); // Green glow
            }
            else if (isPartiallyConfirmed)
            {
                bgColor = Color.FromRgb(255, 249, 196); // Light yellow
                glowColor = Color.FromRgb(255, 193, 7); // Yellow glow
                shouldPulse = true;
            }
            else
            {
                bgColor = Color.FromRgb(227, 242, 253); // Light blue
                glowColor = Color.FromRgb(33, 150, 243); // Blue glow
                shouldPulse = true;
            }

            var row = new Border
            {
                Background = new SolidColorBrush(bgColor),
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(4, 2, 4, 2),
                Cursor = Cursors.Hand
            };

            // Add pulsing glow effect for planned/partially confirmed deliveries
            if (shouldPulse)
            {
                var glowEffect = new DropShadowEffect
                {
                    Color = glowColor,
                    BlurRadius = 6,
                    ShadowDepth = 0,
                    Opacity = 0.3
                };
                row.Effect = glowEffect;

                var pulseOpacity = new DoubleAnimation
                {
                    From = 0.2,
                    To = 0.6,
                    Duration = TimeSpan.FromMilliseconds(900),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                var pulseBlur = new DoubleAnimation
                {
                    From = 3,
                    To = 10,
                    Duration = TimeSpan.FromMilliseconds(900),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever,
                    EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                };
                glowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, pulseOpacity);
                glowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, pulseBlur);
            }

            // Hover effect
            var originalBgColor = bgColor;
            row.MouseEnter += (s, e) => row.Background = new SolidColorBrush(Color.FromRgb(243, 244, 246));
            row.MouseLeave += (s, e) => row.Background = new SolidColorBrush(originalBgColor);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Hodowca
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) }); // Auto
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) }); // Szt
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // Waga
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(45) }); // Status

            // Hodowca name with indicator
            var hodowcaPrefix = shouldPulse ? "○ " : "✓ ";
            var hodowcaText = new TextBlock
            {
                Text = hodowcaPrefix + delivery.Dostawca,
                FontSize = 10,
                FontWeight = shouldPulse ? FontWeights.Normal : FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = delivery.Dostawca
            };
            Grid.SetColumn(hodowcaText, 0);
            grid.Children.Add(hodowcaText);

            // Auto
            var autaText = new TextBlock
            {
                Text = delivery.Auta.ToString(),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(autaText, 1);
            grid.Children.Add(autaText);

            // Szt (Sztuki)
            var sztukiText = new TextBlock
            {
                Text = delivery.Sztuki.ToString("# ##0"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sztukiText, 2);
            grid.Children.Add(sztukiText);

            // Waga
            var wagaText = new TextBlock
            {
                Text = delivery.Waga.ToString("0.00"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(wagaText, 3);
            grid.Children.Add(wagaText);

            // Status badge with glow
            var statusBg = GetStatusBackground(delivery.Status);
            var statusFg = GetStatusForeground(delivery.Status);
            var statusBorder = new Border
            {
                Background = new SolidColorBrush(statusBg),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(2, 1, 2, 1),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // Add subtle glow to status badge for non-confirmed
            if (shouldPulse)
            {
                statusBorder.Effect = new DropShadowEffect
                {
                    Color = statusBg,
                    BlurRadius = 4,
                    ShadowDepth = 0,
                    Opacity = 0.5
                };
            }

            var statusShort = delivery.Status switch
            {
                "Potwierdzony" => "Potw.",
                "B.Kontr." => "B.Ko.",
                "B.Wolny" => "B.Wo.",
                "Do w." => "Do w.",
                "Planowany" => "Plan.",
                "" or null => "Plan.",
                _ => delivery.Status?.Length > 5 ? delivery.Status.Substring(0, 4) + "." : delivery.Status ?? ""
            };
            var statusText = new TextBlock
            {
                Text = statusShort,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = statusFg,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            statusBorder.Child = statusText;
            Grid.SetColumn(statusBorder, 4);
            grid.Children.Add(statusBorder);

            row.Child = grid;
            return row;
        }

        private Color GetStatusBackground(string status)
        {
            return status?.ToLower() switch
            {
                "potwierdzony" => Color.FromRgb(34, 197, 94), // Green
                "planowany" => Color.FromRgb(59, 130, 246), // Blue
                "b.kontr." => Color.FromRgb(251, 191, 36), // Yellow
                "b.wolny" => Color.FromRgb(251, 191, 36), // Yellow
                "do w." => Color.FromRgb(59, 130, 246), // Blue
                _ => Color.FromRgb(59, 130, 246) // Default blue
            };
        }

        private Brush GetStatusForeground(string status)
        {
            return status?.ToLower() switch
            {
                "b.kontr." => Brushes.Black,
                "b.wolny" => Brushes.Black,
                _ => Brushes.White
            };
        }

        // Przechowuje oryginalne daty z bazy danych
        private Dictionary<DateTime, List<MiniDeliveryItem>> _originalDeliveryDates = new();

        /// <summary>
        /// Aktualizuje mini-kalendarz z podglądem planowanych dostaw z formularza
        /// </summary>
        // Debounce: zapamiętaj że trzeba odświeżyć, ale wykonaj dopiero po 300ms bezruchu
        // (zamiast pełnego rebuildu na każdy keystroke).
        private void ScheduleMiniCalendarRefresh()
        {
            if (chkKalendarz?.IsChecked != true) return;

            if (_miniCalendarDebounceTimer == null)
            {
                _miniCalendarDebounceTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300)
                };
                _miniCalendarDebounceTimer.Tick += (s, e) =>
                {
                    _miniCalendarDebounceTimer.Stop();
                    UpdateMiniCalendarPreview();
                };
            }

            // Restart timera - jeśli user dalej pisze, czekamy
            _miniCalendarDebounceTimer.Stop();
            _miniCalendarDebounceTimer.Start();
        }

        private void UpdateMiniCalendarPreview()
        {
            if (chkKalendarz?.IsChecked != true || panelWeeksList == null) return;

            // Skopiuj oryginalne dane z bazy - deep copy
            _deliveryDates = new Dictionary<DateTime, List<MiniDeliveryItem>>();
            foreach (var kvp in _originalDeliveryDates)
            {
                _deliveryDates[kvp.Key] = new List<MiniDeliveryItem>(kvp.Value);
            }

            // Dodaj planowane dostawy z formularza
            foreach (var row in dostawyRows)
            {
                if (row.DpData?.SelectedDate != null)
                {
                    var date = row.DpData.SelectedDate.Value.Date;
                    int szt = 0;
                    int auta = 0;
                    decimal waga = 0;

                    int.TryParse(row.TxtSztuki?.Text?.Replace(" ", ""), out szt);
                    int.TryParse(row.TxtAutaReczne?.Text, out auta);
                    decimal.TryParse(row.TxtWaga?.Text?.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out waga);

                    if (szt > 0)
                    {
                        if (!_deliveryDates.ContainsKey(date))
                            _deliveryDates[date] = new List<MiniDeliveryItem>();

                        _deliveryDates[date].Add(new MiniDeliveryItem
                        {
                            Dostawca = cmbDostawca.SelectedItem?.ToString() ?? "Nowy",
                            Auta = auta,
                            Sztuki = szt,
                            Waga = waga,
                            Status = "Planowany"
                        });
                    }
                }
            }

            // Przerenderuj tygodnie
            FindWeeksWithDeliveries();

            // Spróbuj przewinąć do tygodnia z pierwszą planowaną dostawą
            var firstPlannedDate = dostawyRows
                .Where(r => r.DpData?.SelectedDate != null)
                .Select(r => r.DpData.SelectedDate.Value.Date)
                .OrderBy(d => d)
                .FirstOrDefault();

            if (firstPlannedDate != default)
            {
                var weekStart = GetStartOfWeek(firstPlannedDate);
                var weekIndex = _weeksWithDeliveries.FindIndex(w => w.WeekStart == weekStart);
                if (weekIndex >= 0)
                    _currentWeekIndex = weekIndex;
            }

            RenderWeeksList();
        }

        /// <summary>
        /// Odświeża kalendarz po dodaniu/zmianie dostawy
        /// </summary>
        public void RefreshCalendarWithCurrentDeliveries()
        {
            UpdateMiniCalendarPreview();
        }

        #endregion

        private void AktualizujWstawienie(SqlConnection conn, SqlTransaction trans, long lpW, string typUmowy, string typCeny, int ilosc)
        {
            const string sql = @"UPDATE dbo.WstawieniaKurczakow SET Dostawca = @D, DataWstawienia = @DW, IloscWstawienia = @Il, Uwagi = @Uw, TypUmowy = @TU, TypCeny = @TC, DataMod = SYSDATETIME(), KtoMod = @Kto WHERE Lp = @Lp";
            using (var cmd = new SqlCommand(sql, conn, trans))
            {
                cmd.Parameters.AddWithValue("@Lp", lpW);
                cmd.Parameters.AddWithValue("@D", cmbDostawca.SelectedItem.ToString());
                cmd.Parameters.AddWithValue("@DW", dpDataWstawienia.SelectedDate.Value);
                cmd.Parameters.AddWithValue("@Il", ilosc);
                cmd.Parameters.AddWithValue("@Uw", txtNotatki?.Text ?? "");
                cmd.Parameters.AddWithValue("@TU", typUmowy);
                cmd.Parameters.AddWithValue("@TC", typCeny);
                cmd.Parameters.AddWithValue("@Kto", UserID ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private void UsunStareDostawy(SqlConnection conn, SqlTransaction trans, long lpW)
        {
            using (var cmd = new SqlCommand("DELETE FROM dbo.HarmonogramDostaw WHERE LpW = @LpW", conn, trans))
            {
                cmd.Parameters.AddWithValue("@LpW", lpW);
                cmd.ExecuteNonQuery();
            }
        }

        // Bezpieczna synchronizacja dostaw przy edycji wstawienia.
        // Zachowuje istniejące Lp w HarmonogramDostaw aby nie zerwać relacji z PartiaDostawca/QC/itp.
        private void SynchronizujDostawy(SqlConnection conn, SqlTransaction trans, long lpW,
            DateTime dataWstawienia, string typUmowy, string typCeny, string bufor, bool zapiszAuta)
        {
            // 1. Pobierz Lp dostaw aktualnie istniejących w bazie dla tego wstawienia
            // CAST + Convert.ToInt64 - kolumna Lp w HarmonogramDostaw jest INT, nie BIGINT
            var istniejaceLp = new HashSet<long>();
            using (var cmd = new SqlCommand("SELECT CAST(Lp AS BIGINT) AS Lp FROM dbo.HarmonogramDostaw WHERE LpW = @LpW", conn, trans))
            {
                cmd.Parameters.AddWithValue("@LpW", lpW);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read()) istniejaceLp.Add(Convert.ToInt64(r[0]));
                }
            }

            // 2. UPDATE/INSERT dla każdej dostawy w UI
            var lpZachowane = new HashSet<long>();
            foreach (var dostawa in dostawyRows)
            {
                if (dostawa.LpDostawy.HasValue && istniejaceLp.Contains(dostawa.LpDostawy.Value))
                {
                    AktualizujDostaweDb(conn, trans, dostawa, dostawa.LpDostawy.Value, lpW,
                        dataWstawienia, typUmowy, typCeny, bufor, zapiszAuta);
                    lpZachowane.Add(dostawa.LpDostawy.Value);
                }
                else
                {
                    long noweLp = WstawDostaweDb(conn, trans, dostawa, lpW,
                        dataWstawienia, typUmowy, typCeny, bufor, zapiszAuta);
                    dostawa.LpDostawy = noweLp;
                    lpZachowane.Add(noweLp);
                }
            }

            // 3. DELETE dostaw usuniętych w UI
            foreach (var lp in istniejaceLp)
            {
                if (!lpZachowane.Contains(lp))
                {
                    using (var cmd = new SqlCommand("DELETE FROM dbo.HarmonogramDostaw WHERE Lp = @Lp", conn, trans))
                    {
                        cmd.Parameters.AddWithValue("@Lp", lp);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        private void AktualizujDostaweDb(SqlConnection conn, SqlTransaction trans, DostawaRow dostawa,
            long lpDostawy, long lpW, DateTime dataWstawienia, string typUmowy, string typCeny, string bufor, bool zapiszAuta)
        {
            DateTime dataDostawy = dostawa.DpData?.SelectedDate ?? dataWstawienia;

            const string sql = @"UPDATE dbo.HarmonogramDostaw SET
                Dostawca = @D, DataOdbioru = @DO, KmH = @KmH, WagaDek = @W, SztukiDek = @Szt,
                TypUmowy = @TU, bufor = @Buf, SztSzuflada = @Szuf, Auta = @Auta, typCeny = @TC,
                UWAGI = @Uw
                WHERE Lp = @Lp";
            using (var cmd = new SqlCommand(sql, conn, trans))
            {
                cmd.Parameters.AddWithValue("@Lp", lpDostawy);
                cmd.Parameters.AddWithValue("@D", cmbDostawca.SelectedItem.ToString());
                cmd.Parameters.AddWithValue("@DO", dataDostawy);
                cmd.Parameters.AddWithValue("@KmH", txtKmH?.Text ?? "");

                string wagaText = (dostawa.TxtWaga?.Text ?? "").Replace(',', '.');
                decimal waga = 0m;
                if (!string.IsNullOrWhiteSpace(wagaText))
                    decimal.TryParse(wagaText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out waga);
                cmd.Parameters.AddWithValue("@W", waga);

                int sztuki = 0;
                if (!string.IsNullOrWhiteSpace(dostawa.TxtSztuki?.Text))
                    int.TryParse(dostawa.TxtSztuki.Text, out sztuki);
                cmd.Parameters.AddWithValue("@Szt", sztuki);

                cmd.Parameters.AddWithValue("@TU", typUmowy);
                cmd.Parameters.AddWithValue("@Buf", bufor);

                int sztPoj = 0;
                if (!string.IsNullOrWhiteSpace(dostawa.TxtSztPoj?.Text))
                    int.TryParse(dostawa.TxtSztPoj.Text, out sztPoj);
                cmd.Parameters.AddWithValue("@Szuf", sztPoj);

                int auta = 0;
                if (zapiszAuta && !string.IsNullOrWhiteSpace(dostawa.TxtAutaReczne?.Text))
                    int.TryParse(dostawa.TxtAutaReczne.Text, out auta);
                cmd.Parameters.AddWithValue("@Auta", auta);

                cmd.Parameters.AddWithValue("@TC", typCeny);
                cmd.Parameters.AddWithValue("@Uw", txtNotatki?.Text ?? "");
                cmd.ExecuteNonQuery();
            }
        }

        private int DodajNotatke(SqlConnection conn, SqlTransaction trans, long indeksId, string tresc, string userId)
        {
            if (string.IsNullOrWhiteSpace(tresc))
                return 0;

            const string sql = @"
                INSERT INTO [LibraNet].[dbo].[Notatki] (IndeksID, TypID, Tresc, KtoStworzyl, DataUtworzenia)
                VALUES (@IndeksID, @TypID, @Tresc, @KtoStworzyl, SYSDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS int);";

            using var cmd = new SqlCommand(sql, conn, trans);
            cmd.Parameters.AddWithValue("@IndeksID", indeksId);
            cmd.Parameters.AddWithValue("@TypID", 1);
            cmd.Parameters.AddWithValue("@Tresc", tresc);
            cmd.Parameters.AddWithValue("@KtoStworzyl", userId ?? "");

            object result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        private long WstawDostaweDb(SqlConnection conn, SqlTransaction trans, DostawaRow dostawa, long lpW, DateTime dataWstawienia, string typUmowy, string typCeny, string bufor, bool zapiszAuta)
        {
            long lp;
            // UPDLOCK + HOLDLOCK aby równoległe transakcje nie dostały tego samego Lp
            using (var cmd = new SqlCommand("SELECT MAX(Lp) FROM dbo.HarmonogramDostaw WITH (UPDLOCK, HOLDLOCK)", conn, trans))
            {
                object v = cmd.ExecuteScalar();
                lp = (v == DBNull.Value) ? 1 : Convert.ToInt64(v) + 1;
            }

            // DpData jest jedynym źródłem prawdy - PrzeliczenieDoby już synchronizuje DpData
            // przy edycji TxtDoba, więc nie trzeba ponownie liczyć z TxtDoba (które bywa nieaktualne
            // gdy użytkownik zmienia DatePicker - PrzeliczenieRoznicyDni nie aktualizuje TxtDoba).
            DateTime dataDostawy = dostawa.DpData?.SelectedDate ?? dataWstawienia;

            const string sql = @"INSERT INTO dbo.HarmonogramDostaw (Lp, LpW, Dostawca, DataOdbioru, Kmk, KmH, WagaDek, SztukiDek, TypUmowy, bufor, SztSzuflada, Auta, typCeny, UWAGI, DataUtw, KtoStwo)
                VALUES (@Lp, @LpW, @D, @DO, @KmK, @KmH, @W, @Szt, @TU, @Buf, @Szuf, @Auta, @TC, @Uw, SYSDATETIME(), @Kto)";
            using (var cmd = new SqlCommand(sql, conn, trans))
            {
                cmd.Parameters.AddWithValue("@Lp", lp);
                cmd.Parameters.AddWithValue("@LpW", lpW);
                cmd.Parameters.AddWithValue("@D", cmbDostawca.SelectedItem.ToString());
                cmd.Parameters.AddWithValue("@DO", dataDostawy);
                cmd.Parameters.AddWithValue("@KmK", "");
                cmd.Parameters.AddWithValue("@KmH", txtKmH?.Text ?? "");

                string wagaText = (dostawa.TxtWaga?.Text ?? "").Replace(',', '.');
                decimal waga = 0m;
                if (!string.IsNullOrWhiteSpace(wagaText))
                    decimal.TryParse(wagaText, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out waga);
                cmd.Parameters.AddWithValue("@W", waga);

                int sztuki = 0;
                if (!string.IsNullOrWhiteSpace(dostawa.TxtSztuki?.Text))
                {
                    int.TryParse(dostawa.TxtSztuki.Text, out sztuki);
                }
                cmd.Parameters.AddWithValue("@Szt", sztuki);

                cmd.Parameters.AddWithValue("@TU", typUmowy);
                cmd.Parameters.AddWithValue("@Buf", bufor);

                int sztPoj = 0;
                if (!string.IsNullOrWhiteSpace(dostawa.TxtSztPoj?.Text))
                {
                    int.TryParse(dostawa.TxtSztPoj.Text, out sztPoj);
                }
                cmd.Parameters.AddWithValue("@Szuf", sztPoj);

                int auta = 0;
                if (zapiszAuta && !string.IsNullOrWhiteSpace(dostawa.TxtAutaReczne?.Text))
                {
                    int.TryParse(dostawa.TxtAutaReczne.Text, out auta);
                }
                cmd.Parameters.AddWithValue("@Auta", auta);

                cmd.Parameters.AddWithValue("@TC", typCeny);
                cmd.Parameters.AddWithValue("@Uw", txtNotatki?.Text ?? "");
                cmd.Parameters.AddWithValue("@Kto", UserID ?? "");
                cmd.ExecuteNonQuery();
            }

            // DODAJ NOTATKĘ DO TABELI NOTATKI
            if (!string.IsNullOrWhiteSpace(txtNotatki?.Text))
            {
                int noteId = DodajNotatke(conn, trans, lp, txtNotatki.Text, UserID ?? "");
                if (noteId > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Dodano notatkę ID: {noteId} dla dostawy LP: {lp}");
                }
            }

            return lp;
        }

        #endregion

        #region Przyciski

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Anulować?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                DialogResult = false;
                Close();
            }
        }

        #endregion

        #region Klasy

        public class DostawaRow
        {
            public int Id { get; set; }
            public long? LpDostawy { get; set; }
            public int? Doba { get; set; }
            public double? Waga { get; set; }
            public int? SztPoj { get; set; }
            public int? Sztuki { get; set; }
            public int? Auta { get; set; }
            public TextBlock TxtNr { get; set; }
            public TextBox TxtDoba { get; set; }
            public DatePicker DpData { get; set; }
            public TextBox TxtDni { get; set; }
            public TextBox TxtWaga { get; set; }
            public TextBox TxtSztPoj { get; set; }
            public TextBox TxtMnoznik { get; set; }
            public TextBox TxtSztuki { get; set; }
            public TextBox TxtAutaReczne { get; set; }
            public TextBox TxtAutaWyliczone { get; set; }
        }

        public class SeriaWstawienRow
        {
            public int Id { get; set; }
            public DatePicker DpData { get; set; }
            public TextBox TxtIlosc { get; set; }
        }

        public class HarmonogramDostaw
        {
            public long Lp { get; set; }
            public DateTime DataOdbioru { get; set; }
            public int SztukiDek { get; set; }
            public decimal WagaDek { get; set; }
            public int SztSzuflada { get; set; }
            public int Auta { get; set; }
        }

        #endregion
    }

    // NOWE KLASY POMOCNICZE - muszą być dostępne także w WidokWstawienia
    public class DaneDostawy
    {
        public int? Doba { get; set; }
        public double? Waga { get; set; }
        public int? SztPoj { get; set; }
        public int? Auta { get; set; }
        public int? Sztuki { get; set; }
    }

    public class DaneOstatniegoDostarczonego
    {
        public List<DaneDostawy> Dostawy { get; set; }
    }

    /// <summary>
    /// Represents delivery data for a week in the mini calendar
    /// </summary>
    public class MiniWeekData
    {
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public int WeekNumber { get; set; }
        public int TotalSztuki { get; set; }
        public Dictionary<DateTime, List<MiniDeliveryItem>> DayDeliveries { get; set; } = new();
    }

    /// <summary>
    /// Represents a single delivery item for the mini calendar
    /// </summary>
    public class MiniDeliveryItem
    {
        public string Dostawca { get; set; } = "";
        public int Auta { get; set; }
        public int Sztuki { get; set; }
        public decimal Waga { get; set; }
        public string Status { get; set; } = "";
    }
}