using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Media;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace Kalendarz1
{
    public class Odbiorca
    {
        public int ID { get; set; }
        public string Nazwa { get; set; }
        public override string ToString() => Nazwa;
    }

    public partial class PanelPortiera : Window, INotifyPropertyChanged
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private const string FIRMA_NAZWA = "Ubojnia Drobiu \"PIÓRKOWSCY\"";
        private const string FIRMA_ADRES = "Koziołki 40";
        private const string FIRMA_MIASTO = "95-061 Dmosin";
        private const string FIRMA_NIP = "726-162-54-06";
        private const string FIRMA_REGON = "750045476";
        private const string FIRMA_TEL = "(46) 874 71 70";

        private ObservableCollection<DostawaPortiera> dostawy;
        private DostawaPortiera _wybranaDostwa;
        private Border selectedCardBorder = null;
        private DataGridRow selectedGridRow = null;

        private const string EXIT_PIN = "1994";
        private string currentPin = "";

        private int originalBrutto = 0;
        private int originalTara = 0;
        private string aktualnyTowar = "KREW";
        
        // Zmienne do przechowywania aktualnie wpisywanych wartości (niezależne od bazy)
        private int wpisywaneBrutto = 0;
        private int wpisywaneTara = 0;
        
        // Flagi do śledzenia czy pierwsza cyfra przy edycji (ma wyzerować wyświetlacz)
        private bool czekaNaPierwszaCyfreBrutto = false;
        private bool czekaNaPierwszaCyfreTara = false;

        public DostawaPortiera WybranaDostwa
        {
            get => _wybranaDostwa;
            set { _wybranaDostwa = value; OnPropertyChanged(nameof(WybranaDostwa)); }
        }

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate != value)
                {
                    _selectedDate = value;
                    OnPropertyChanged(nameof(SelectedDate));
                    LoadDostawy();
                }
            }
        }

        private enum AktywnePole { Brutto, Tara }
        private AktywnePole aktywnePole = AktywnePole.Tara;
        private string aktualnyTryb = "Avilog";

        private SerialPort serialPort;
        private DispatcherTimer autoRefreshTimer;
        private DispatcherTimer clockTimer;
        private DispatcherTimer dateCheckTimer;

        public ObservableCollection<Odbiorca> ListaOdbiorcow { get; set; } = new ObservableCollection<Odbiorca>();

        private int nextWzNumber = 1;
        private SoundPlayer soundSuccess;
        private SoundPlayer soundError;

        public PanelPortiera()
        {
            InitializeComponent();
            System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("pl-PL");
            System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo("pl-PL");
            DataContext = this;

            dostawy = new ObservableCollection<DostawaPortiera>();
            listDostawy.ItemsSource = dostawy;
            gridTable.ItemsSource = dostawy;
            cbOdbiorcy.ItemsSource = ListaOdbiorcow;
            cbOdbiorcy.DisplayMemberPath = "Nazwa";

            try
            {
                soundSuccess = new SoundPlayer(@"C:\Windows\Media\chimes.wav");
                soundError = new SoundPlayer(@"C:\Windows\Media\chord.wav");
            }
            catch { }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dpDate.SelectedDate = DateTime.Today;
            LoadDostawy();
            LoadNextWzNumber();

            autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            autoRefreshTimer.Tick += (s, ev) => LoadDostawy();
            autoRefreshTimer.Start();

            // ZMIANA: Zegar bez animacji migania, odswiezanie co 1 sekunde
            clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clockTimer.Tick += (s, ev) =>
            {
                // clockShowColon = !clockShowColon; // USUNIETO
                lblTime.Text = DateTime.Now.ToString("HH:mm");
            };
            clockTimer.Start();
            lblTime.Text = DateTime.Now.ToString("HH:mm");

            dateCheckTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(10) };
            dateCheckTimer.Tick += (s, ev) =>
            {
                if (DateTime.Now.Hour >= 22)
                {
                    var nextDay = DateTime.Today.AddDays(1);
                    if (SelectedDate.Date != nextDay)
                    {
                        SelectedDate = nextDay;
                        dpDate.SelectedDate = nextDay;
                    }
                }
            };
            dateCheckTimer.Start();

            ConnectToScale("COM3", 9600);
        }

        #region ANIMACJE

        private void AnimateGlow(Border border, bool start)
        {
            if (border == null) return;

            if (start)
            {
                var glow = new DropShadowEffect
                {
                    Color = (Color)ColorConverter.ConvertFromString("#FFA726"),
                    BlurRadius = 20,
                    ShadowDepth = 0,
                    Opacity = 0.8
                };
                border.Effect = glow;

                var glowAnim = new DoubleAnimation
                {
                    From = 15,
                    To = 35,
                    Duration = TimeSpan.FromSeconds(0.6),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, glowAnim);

                var bgAnim = new ColorAnimation
                {
                    From = (Color)ColorConverter.ConvertFromString("#1E1E2E"),
                    To = (Color)ColorConverter.ConvertFromString("#2A2A3E"),
                    Duration = TimeSpan.FromSeconds(0.6),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E2E"));
                border.Background.BeginAnimation(SolidColorBrush.ColorProperty, bgAnim);
            }
            else
            {
                border.Effect?.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
                border.Background?.BeginAnimation(SolidColorBrush.ColorProperty, null);
                border.Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 10, Opacity = 0.3, ShadowDepth = 2 };
                border.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E2E"));
            }
        }

        private void AnimateRowGlow(DataGridRow row, bool start)
        {
            // Animacja jest teraz obsługiwana przez styl XAML
            // Ta metoda pozostaje pusta dla kompatybilności
        }

        private void AnimateSuccess(Border card)
        {
            if (card == null) return;

            var flash = new ColorAnimation
            {
                From = (Color)ColorConverter.ConvertFromString("#4CAF50"),
                To = (Color)ColorConverter.ConvertFromString("#1E1E2E"),
                Duration = TimeSpan.FromSeconds(0.5)
            };
            card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
            card.Background.BeginAnimation(SolidColorBrush.ColorProperty, flash);
        }

        private void PlaySound(bool success)
        {
            try
            {
                if (success) soundSuccess?.Play();
                else soundError?.Play();
            }
            catch { }
        }

        #endregion

        #region KLAWIATURA FIZYCZNA

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (KeyboardOverlay.Visibility == Visibility.Visible) return;
            if (PasswordOverlay.Visibility == Visibility.Visible) return;
            if (aktualnyTryb == "Avilog" && WybranaDostwa == null) return;

            string key = e.Key.ToString();

            if (key.Length == 2 && key.StartsWith("D") && char.IsDigit(key[1]))
            {
                AddDigitToWeight(key[1].ToString());
                e.Handled = true;
            }
            else if (key.StartsWith("NumPad") && key.Length == 7)
            {
                AddDigitToWeight(key[6].ToString());
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                BackspaceWeight();
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                ClearCurrentWeight();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                BtnZapisz_Click(null, null);
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                ToggleTaraBrutto();
                e.Handled = true;
            }
        }

        private void AddDigitToWeight(string digit)
        {
            TextBlock target = (aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara;
            
            // Sprawdź czy czeka na pierwszą cyfrę (edycja istniejącego wpisu)
            bool czyZerowac = (aktywnePole == AktywnePole.Brutto) ? czekaNaPierwszaCyfreBrutto : czekaNaPierwszaCyfreTara;
            
            if (czyZerowac)
            {
                target.Text = "";
                if (aktywnePole == AktywnePole.Brutto)
                    czekaNaPierwszaCyfreBrutto = false;
                else
                    czekaNaPierwszaCyfreTara = false;
            }
            else if (target.Text == "0")
            {
                target.Text = "";
            }
            
            if (target.Text.Length < 6) target.Text += digit;
            
            // Aktualizuj odpowiednią zmienną wpisywaną
            if (aktywnePole == AktywnePole.Brutto)
                int.TryParse(txtBrutto.Text, out wpisywaneBrutto);
            else
                int.TryParse(txtTara.Text, out wpisywaneTara);
            
            UpdateBigDisplay();
        }

        private void BackspaceWeight()
        {
            TextBlock target = (aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara;
            if (target.Text.Length > 0) target.Text = target.Text.Substring(0, target.Text.Length - 1);
            if (string.IsNullOrEmpty(target.Text)) target.Text = "0";
            
            // Wyłącz flagę jeśli użyto backspace
            if (aktywnePole == AktywnePole.Brutto)
            {
                czekaNaPierwszaCyfreBrutto = false;
                int.TryParse(txtBrutto.Text, out wpisywaneBrutto);
            }
            else
            {
                czekaNaPierwszaCyfreTara = false;
                int.TryParse(txtTara.Text, out wpisywaneTara);
            }
            
            UpdateBigDisplay();
        }

        private void ClearCurrentWeight()
        {
            TextBlock target = (aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara;
            target.Text = "0";
            
            // Wyłącz flagę i wyczyść zmienną
            if (aktywnePole == AktywnePole.Brutto)
            {
                czekaNaPierwszaCyfreBrutto = false;
                wpisywaneBrutto = 0;
            }
            else
            {
                czekaNaPierwszaCyfreTara = false;
                wpisywaneTara = 0;
            }
            
            UpdateBigDisplay();
        }

        private void ToggleTaraBrutto()
        {
            // Zapisz aktualną wartość przed przełączeniem
            if (aktywnePole == AktywnePole.Brutto)
                int.TryParse(txtBrutto.Text, out wpisywaneBrutto);
            else
                int.TryParse(txtTara.Text, out wpisywaneTara);
            
            // Przełącz
            if (radioBrutto.IsChecked == true)
            {
                radioTara.IsChecked = true;
                aktywnePole = AktywnePole.Tara;
                txtTara.Text = wpisywaneTara.ToString();
            }
            else
            {
                radioBrutto.IsChecked = true;
                aktywnePole = AktywnePole.Brutto;
                txtBrutto.Text = wpisywaneBrutto.ToString();
            }
            UpdateBigDisplay();
        }

        #endregion

        #region ODBIORCY

        private void LoadOdbiorcyDlaTowar(string towar)
        {
            ListaOdbiorcow.Clear();

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"SELECT ID, Nazwa FROM dbo.OdpadyOdbiorcy WHERE (Towar = @Towar OR Towar IS NULL) AND Aktywny = 1 ORDER BY Nazwa";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Towar", towar);
                        using (var r = cmd.ExecuteReader())
                        {
                            while (r.Read())
                            {
                                ListaOdbiorcow.Add(new Odbiorca { ID = Convert.ToInt32(r["ID"]), Nazwa = r["Nazwa"].ToString() });
                            }
                        }
                    }
                }
            }
            catch { }

            if (ListaOdbiorcow.Count == 0)
            {
                if (towar == "KREW" || towar == "JELITA")
                {
                    ListaOdbiorcow.Add(new Odbiorca { ID = 1, Nazwa = "Jasta" });
                    ListaOdbiorcow.Add(new Odbiorca { ID = 2, Nazwa = "General Food Supply" });
                }
                else if (towar == "LAPY")
                {
                    ListaOdbiorcow.Add(new Odbiorca { ID = 1, Nazwa = "Jasta" });
                    ListaOdbiorcow.Add(new Odbiorca { ID = 3, Nazwa = "Marcin Piorkowski" });
                }
                else if (towar == "ODPADY")
                {
                    ListaOdbiorcow.Add(new Odbiorca { ID = 1, Nazwa = "Jasta" });
                    ListaOdbiorcow.Add(new Odbiorca { ID = 4, Nazwa = "Utylizacja" });
                }
                else
                {
                    ListaOdbiorcow.Add(new Odbiorca { ID = 1, Nazwa = "Jasta" });
                }
            }

            // Ustaw Jasta jako domyślnego odbiorcę
            var jasta = ListaOdbiorcow.FirstOrDefault(x => x.Nazwa == "Jasta");
            if (jasta != null)
                cbOdbiorcy.SelectedItem = jasta;
            else if (ListaOdbiorcow.Count > 0) 
                cbOdbiorcy.SelectedIndex = 0;
        }

        #endregion

        #region ZMIANA TOWARU

        private void UpdateThemeColor(string towar)
        {
            Color color = towar switch
            {
                "KREW" => (Color)ColorConverter.ConvertFromString("#D32F2F"),
                "LAPY" => (Color)ColorConverter.ConvertFromString("#FFC107"),
                "PIORA" => (Color)ColorConverter.ConvertFromString("#9E9E9E"),
                "JELITA" => (Color)ColorConverter.ConvertFromString("#795548"),
                "ODPADY" => (Color)ColorConverter.ConvertFromString("#AB47BC"),
                _ => (Color)ColorConverter.ConvertFromString("#FFA726")
            };
            this.Resources["ThemeColor"] = new SolidColorBrush(color);
        }

        public void BtnCommodity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
                if (rb == btnKrew) aktualnyTowar = "KREW";
                else if (rb == btnLapy) aktualnyTowar = "LAPY";
                else if (rb == btnJelita) aktualnyTowar = "JELITA";
                else if (rb == btnPiora) aktualnyTowar = "PIORA";
                else if (rb == btnOdpady) aktualnyTowar = "ODPADY";

                UpdateThemeColor(aktualnyTowar);
                LoadOdbiorcyDlaTowar(aktualnyTowar);

                if (WybranaDostwa != null && WybranaDostwa.ID <= 0)
                    WybranaDostwa.Towar = aktualnyTowar;
            }
        }

        #endregion

        #region KLAWIATURA EKRANOWA

        private void TxtEditRejestracja_Click(object sender, MouseButtonEventArgs e)
        {
            KeyboardOverlay.Visibility = Visibility.Visible;
        }

        private void CloseKeyboard_Click(object sender, RoutedEventArgs e)
        {
            KeyboardOverlay.Visibility = Visibility.Collapsed;
        }

        private void KeyboardOverlay_Click(object sender, MouseButtonEventArgs e)
        {
            KeyboardOverlay.Visibility = Visibility.Collapsed;
        }

        private void KeyboardPanel_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void Keyboard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                txtEditRejestracja.Text += btn.Content.ToString();
        }

        private void KeyboardBackspace_Click(object sender, RoutedEventArgs e)
        {
            if (txtEditRejestracja.Text.Length > 0)
                txtEditRejestracja.Text = txtEditRejestracja.Text.Substring(0, txtEditRejestracja.Text.Length - 1);
        }

        #endregion

        #region PIN / WYJSCIE

        public void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            currentPin = "";
            UpdatePinDisplay();
            PasswordOverlay.Visibility = Visibility.Visible;
        }

        private void UpdatePinDisplay()
        {
            string display = "";
            for (int i = 0; i < 4; i++)
                display += (i < currentPin.Length) ? "*" : "_";
            txtPinDisplay.Text = display;
        }

        private void PinNumpadClick(object sender, RoutedEventArgs e)
        {
            if (currentPin.Length >= 4) return;
            if (sender is Button btn)
            {
                currentPin += btn.Content.ToString();
                UpdatePinDisplay();

                if (currentPin.Length == 4)
                {
                    if (currentPin == EXIT_PIN)
                    {
                        PasswordOverlay.Visibility = Visibility.Collapsed;
                        if (serialPort != null && serialPort.IsOpen) serialPort.Close();
                        this.Close();
                    }
                    else
                    {
                        PlaySound(false);
                        txtPinDisplay.Foreground = Brushes.Red;
                        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
                        timer.Tick += (s, ev) =>
                        {
                            txtPinDisplay.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA726"));
                            timer.Stop();
                        };
                        timer.Start();
                        currentPin = "";
                        UpdatePinDisplay();
                    }
                }
            }
        }

        private void PinClear_Click(object sender, RoutedEventArgs e)
        {
            currentPin = "";
            UpdatePinDisplay();
        }

        private void PinBackspace_Click(object sender, RoutedEventArgs e)
        {
            if (currentPin.Length > 0)
                currentPin = currentPin.Substring(0, currentPin.Length - 1);
            UpdatePinDisplay();
        }

        private void PinCancel_Click(object sender, RoutedEventArgs e)
        {
            currentPin = "";
            PasswordOverlay.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region NAWIGACJA

        public void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDostawy();
        }

        public void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DatePicker dp && dp.SelectedDate.HasValue)
                SelectedDate = dp.SelectedDate.Value;
        }

        public void ZmienTryb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
                if (rb.Name == "rbAvilog")
                {
                    aktualnyTryb = "Avilog";
                    this.Resources["ThemeColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA726"));
                    viewTiles.Visibility = Visibility.Visible;
                    gridTable.Visibility = Visibility.Collapsed;
                    panelButtonsLeft.Visibility = Visibility.Collapsed;
                    panelCommodity.Visibility = Visibility.Collapsed;
                    panelReadOnlyCar.Visibility = Visibility.Visible;
                    panelEditCar.Visibility = Visibility.Collapsed;
                    btnScanAvilog.Visibility = Visibility.Visible;
                    btnPrintAvilog.Visibility = Visibility.Visible;
                }
                else if (rb.Name == "rbOdpady")
                {
                    aktualnyTryb = "Odpady";
                    aktualnyTowar = "KREW";
                    UpdateThemeColor(aktualnyTowar);
                    viewTiles.Visibility = Visibility.Collapsed;
                    gridTable.Visibility = Visibility.Visible;
                    panelButtonsLeft.Visibility = Visibility.Visible;
                    panelCommodity.Visibility = Visibility.Visible;
                    panelReadOnlyCar.Visibility = Visibility.Collapsed;
                    panelEditCar.Visibility = Visibility.Visible;
                    btnScanAvilog.Visibility = Visibility.Collapsed;
                    btnPrintAvilog.Visibility = Visibility.Collapsed;
                    btnKrew.IsChecked = true;
                    LoadOdbiorcyDlaTowar(aktualnyTowar);
                }
                LoadDostawy();
                ClearFormularz();
            }
        }

        #endregion

        #region LADOWANIE DANYCH

        private void LoadDostawy()
        {
            try
            {
                if (selectedCardBorder != null)
                {
                    AnimateGlow(selectedCardBorder, false);
                    selectedCardBorder = null;
                }
                if (selectedGridRow != null)
                {
                    AnimateRowGlow(selectedGridRow, false);
                    selectedGridRow = null;
                }

                dostawy.Clear();

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    if (aktualnyTryb == "Avilog")
                    {
                        string query = @"SELECT fc.ID, fc.LpDostawy, fc.CustomerGID,
                            (SELECT TOP 1 ShortName FROM dbo.Dostawcy WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(fc.CustomerGID))) as HodowcaNazwa,
                            ISNULL(dr.[Name], '') as KierowcaNazwa, fc.CarID, fc.TrailerID, fc.SztPoj,
                            ISNULL(fc.FullFarmWeight, 0) as Brutto, ISNULL(fc.EmptyFarmWeight, 0) as Tara, 
                            ISNULL(fc.NettoFarmWeight, 0) as Netto, fc.Przyjazd, fc.GodzinaTara, fc.GodzinaBrutto
                        FROM dbo.FarmerCalc fc 
                        LEFT JOIN dbo.Driver dr ON fc.DriverGID = dr.GID
                        WHERE CAST(fc.CalcDate AS DATE) = @Data
                        ORDER BY CASE WHEN ISNUMERIC(fc.LpDostawy) = 1 THEN CAST(fc.LpDostawy AS INT) ELSE 999999 END, fc.ID";

                        using (var cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Data", SelectedDate);
                            using (var r = cmd.ExecuteReader())
                            {
                                int lp = 1;
                                while (r.Read())
                                {
                                    var przyjazd = r["Przyjazd"] != DBNull.Value ? Convert.ToDateTime(r["Przyjazd"]) : (DateTime?)null;
                                    var godzTara = r["GodzinaTara"] != DBNull.Value ? Convert.ToDateTime(r["GodzinaTara"]) : (DateTime?)null;
                                    var godzBrutto = r["GodzinaBrutto"] != DBNull.Value ? Convert.ToDateTime(r["GodzinaBrutto"]) : (DateTime?)null;

                                    var d = new DostawaPortiera
                                    {
                                        ID = Convert.ToInt64(r["ID"]),
                                        Lp = (lp++).ToString(),
                                        HodowcaNazwa = r["HodowcaNazwa"]?.ToString() ?? "Nieznany",
                                        KierowcaNazwa = r["KierowcaNazwa"]?.ToString() ?? "",
                                        CarID = r["CarID"]?.ToString() ?? "",
                                        TrailerID = r["TrailerID"]?.ToString() ?? "",
                                        Brutto = r["Brutto"] != DBNull.Value ? Convert.ToInt32(Convert.ToDecimal(r["Brutto"])) : 0,
                                        Tara = r["Tara"] != DBNull.Value ? Convert.ToInt32(Convert.ToDecimal(r["Tara"])) : 0,
                                        Netto = r["Netto"] != DBNull.Value ? Convert.ToInt32(Convert.ToDecimal(r["Netto"])) : 0,
                                        SztukiPlan = r["SztPoj"] != DBNull.Value ? Convert.ToInt32(Convert.ToDecimal(r["SztPoj"])) : 0,
                                        Towar = "Zywiec",
                                        GodzinaPrzyjazdu = przyjazd?.ToString("HH:mm") ?? "00:00",
                                        GodzinaTaraDisplay = godzTara?.ToString("HH:mm") ?? "-",
                                        GodzinaBruttoDisplay = godzBrutto?.ToString("HH:mm") ?? "-"
                                    };
                                    d.NrRejestracyjny = $"{d.CarID} {d.TrailerID}";
                                    dostawy.Add(d);
                                }
                            }
                        }
                    }
                    else
                    {
                        string query = @"SELECT ID, FORMAT(DataWazenia, 'HH:mm') as Godzina, NrRejestracyjny, Odbiorca, 
                                   ISNULL(Brutto, 0) as Brutto, ISNULL(Tara, 0) as Tara, ISNULL(Netto, 0) as Netto, 
                                   ISNULL(Towar, 'KREW') as Towar, GodzinaTara, GodzinaBrutto
                            FROM dbo.OdpadyRejestr WHERE CAST(DataWazenia AS DATE) = @Data ORDER BY DataWazenia DESC";

                        using (var cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Data", DateTime.Today);
                            using (var r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    var godzTara = r["GodzinaTara"] != DBNull.Value ? Convert.ToDateTime(r["GodzinaTara"]) : (DateTime?)null;
                                    var godzBrutto = r["GodzinaBrutto"] != DBNull.Value ? Convert.ToDateTime(r["GodzinaBrutto"]) : (DateTime?)null;

                                    dostawy.Add(new DostawaPortiera
                                    {
                                        ID = Convert.ToInt64(r["ID"]),
                                        GodzinaPrzyjazdu = r["Godzina"]?.ToString() ?? "",
                                        NrRejestracyjny = r["NrRejestracyjny"]?.ToString() ?? "",
                                        HodowcaNazwa = r["Odbiorca"]?.ToString() ?? "",
                                        Brutto = Convert.ToInt32(r["Brutto"]),
                                        Tara = Convert.ToInt32(r["Tara"]),
                                        Netto = Convert.ToInt32(r["Netto"]),
                                        Towar = r["Towar"]?.ToString() ?? "KREW",
                                        GodzinaTaraDisplay = godzTara?.ToString("HH:mm") ?? "-",
                                        GodzinaBruttoDisplay = godzBrutto?.ToString("HH:mm") ?? "-",
                                        Lp = "-",
                                        CarID = "",
                                        TrailerID = "",
                                        KierowcaNazwa = "",
                                        SztukiPlan = 0
                                    });
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Blad ladowania: " + ex.Message);
            }
        }

        private void LoadNextWzNumber()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT ISNULL(MAX(NrWZ), 0) + 1 FROM dbo.DokumentyWZ WHERE YEAR(DataWystawienia) = YEAR(GETDATE())", conn))
                    {
                        var result = cmd.ExecuteScalar();
                        nextWzNumber = result != DBNull.Value ? Convert.ToInt32(result) : 1;
                    }
                }
            }
            catch { nextWzNumber = 1; }
        }

        #endregion

        #region WYBOR WPISU

        public void Dostawa_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is DostawaPortiera dostawa)
            {
                if (selectedCardBorder != null)
                    AnimateGlow(selectedCardBorder, false);

                selectedCardBorder = border;
                AnimateGlow(border, true);

                WybierzDostawe(dostawa);
            }
        }

        private void GridTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gridTable.SelectedItem is DostawaPortiera dostawa)
            {
                if (selectedGridRow != null)
                    AnimateRowGlow(selectedGridRow, false);

                selectedGridRow = gridTable.ItemContainerGenerator.ContainerFromItem(dostawa) as DataGridRow;
                if (selectedGridRow != null)
                    AnimateRowGlow(selectedGridRow, true);

                WybierzDostawe(dostawa);
            }
        }

        private void WybierzDostawe(DostawaPortiera dostawa)
        {
            WybranaDostwa = dostawa;
            originalBrutto = dostawa.Brutto;
            originalTara = dostawa.Tara;
            
            // Inicjalizuj zmienne wpisywane wartościami z bazy
            wpisywaneBrutto = dostawa.Brutto;
            wpisywaneTara = dostawa.Tara;
            
            // Ustaw flagi - jeśli jest wartość, pierwsza cyfra ma wyzerować
            czekaNaPierwszaCyfreBrutto = (dostawa.Brutto > 0);
            czekaNaPierwszaCyfreTara = (dostawa.Tara > 0);

            if (aktualnyTryb == "Avilog")
            {
                panelReadOnlyCar.Visibility = Visibility.Visible;
                panelEditCar.Visibility = Visibility.Collapsed;
                panelCommodity.Visibility = Visibility.Collapsed;
                lblWybranyPojazd.Text = dostawa.CarID;
                lblWybranaNaczepa.Text = dostawa.TrailerID;
                lblWybranyKierowca.Text = dostawa.KierowcaNazwa;
                lblWybranyHodowca.Text = dostawa.HodowcaNazwa;
                lblWybranaGodzina.Text = $"Przyjazd: {dostawa.GodzinaPrzyjazdu}";
            }
            else
            {
                panelReadOnlyCar.Visibility = Visibility.Collapsed;
                panelEditCar.Visibility = Visibility.Visible;
                panelCommodity.Visibility = Visibility.Visible;
                txtEditRejestracja.Text = dostawa.NrRejestracyjny;
                aktualnyTowar = dostawa.Towar;

                if (dostawa.Towar == "KREW") btnKrew.IsChecked = true;
                else if (dostawa.Towar == "LAPY") btnLapy.IsChecked = true;
                else if (dostawa.Towar == "PIORA") btnPiora.IsChecked = true;
                else if (dostawa.Towar == "JELITA") btnJelita.IsChecked = true;
                else if (dostawa.Towar == "ODPADY") btnOdpady.IsChecked = true;
                else btnKrew.IsChecked = true;

                UpdateThemeColor(aktualnyTowar);
                LoadOdbiorcyDlaTowar(aktualnyTowar);

                if (!string.IsNullOrEmpty(dostawa.HodowcaNazwa))
                {
                    var odbiorca = ListaOdbiorcow.FirstOrDefault(x => x.Nazwa == dostawa.HodowcaNazwa);
                    if (odbiorca != null) cbOdbiorcy.SelectedItem = odbiorca;
                }
            }

            txtBrutto.Text = dostawa.Brutto.ToString();
            txtTara.Text = dostawa.Tara.ToString();

            if (dostawa.Tara > 0 && dostawa.Brutto == 0)
            {
                radioBrutto.IsChecked = true;
                aktywnePole = AktywnePole.Brutto;
            }
            else if (dostawa.Brutto > 0 && dostawa.Tara == 0)
            {
                radioTara.IsChecked = true;
                aktywnePole = AktywnePole.Tara;
            }
            else if (dostawa.Tara == 0 && dostawa.Brutto == 0)
            {
                radioTara.IsChecked = true;
                aktywnePole = AktywnePole.Tara;
            }
            else
            {
                radioBrutto.IsChecked = true;
                aktywnePole = AktywnePole.Brutto;
            }

            UpdateBigDisplay();
            btnDelete.IsEnabled = true;
        }

        private void ClearFormularz()
        {
            WybranaDostwa = null;
            originalBrutto = 0;
            originalTara = 0;
            
            // Wyczyść zmienne wpisywane
            wpisywaneBrutto = 0;
            wpisywaneTara = 0;
            
            // Wyczyść flagi
            czekaNaPierwszaCyfreBrutto = false;
            czekaNaPierwszaCyfreTara = false;

            lblWybranyPojazd.Text = "---";
            lblWybranaNaczepa.Text = "---";
            lblWybranyKierowca.Text = "Wybierz pojazd";
            lblWybranyHodowca.Text = "";
            lblWybranaGodzina.Text = "";

            txtBrutto.Text = "0";
            txtTara.Text = "0";
            BigWeightDisplay.Text = "0";
            txtEditRejestracja.Text = "";

            radioTara.IsChecked = true;
            aktywnePole = AktywnePole.Tara;
            btnDelete.IsEnabled = false;
            gridTable.SelectedItem = null;

            if (selectedCardBorder != null)
            {
                AnimateGlow(selectedCardBorder, false);
                selectedCardBorder = null;
            }
            if (selectedGridRow != null)
            {
                AnimateRowGlow(selectedGridRow, false);
                selectedGridRow = null;
            }
        }

        #endregion

        #region NUMPAD

        public void Mode_Click(object sender, RoutedEventArgs e)
        {
            // Zapisz aktualną wartość z wyświetlacza do odpowiedniej zmiennej PRZED przełączeniem
            if (aktywnePole == AktywnePole.Brutto)
            {
                int.TryParse(txtBrutto.Text, out wpisywaneBrutto);
            }
            else
            {
                int.TryParse(txtTara.Text, out wpisywaneTara);
            }
            
            // Przełącz aktywne pole
            aktywnePole = (radioBrutto.IsChecked == true) ? AktywnePole.Brutto : AktywnePole.Tara;
            
            // Pokaż wartość dla nowo wybranego pola
            if (aktywnePole == AktywnePole.Brutto)
            {
                txtBrutto.Text = wpisywaneBrutto.ToString();
            }
            else
            {
                txtTara.Text = wpisywaneTara.ToString();
            }
            
            UpdateBigDisplay();
        }

        private void UpdateBigDisplay()
        {
            string rawValue = (aktywnePole == AktywnePole.Brutto) ? txtBrutto.Text : txtTara.Text;
            if (int.TryParse(rawValue, out int val))
            {
                var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                nfi.NumberGroupSeparator = " ";
                BigWeightDisplay.Text = val.ToString("N0", nfi);
            }
            else
            {
                BigWeightDisplay.Text = "0";
            }
        }

        public void NumpadClick(object sender, RoutedEventArgs e)
        {
            if (aktualnyTryb == "Avilog" && WybranaDostwa == null) return;

            if (sender is Button btn)
            {
                TextBlock target = (aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara;
                
                // Sprawdź czy czeka na pierwszą cyfrę (edycja istniejącego wpisu)
                bool czyZerowac = (aktywnePole == AktywnePole.Brutto) ? czekaNaPierwszaCyfreBrutto : czekaNaPierwszaCyfreTara;
                
                if (czyZerowac)
                {
                    // Zeruj wyświetlacz i wyłącz flagę
                    target.Text = "";
                    if (aktywnePole == AktywnePole.Brutto)
                        czekaNaPierwszaCyfreBrutto = false;
                    else
                        czekaNaPierwszaCyfreTara = false;
                }
                else if (target.Text == "0")
                {
                    target.Text = "";
                }
                
                if (target.Text.Length < 6) target.Text += btn.Content.ToString();
                
                // Aktualizuj odpowiednią zmienną wpisywaną
                if (aktywnePole == AktywnePole.Brutto)
                    int.TryParse(txtBrutto.Text, out wpisywaneBrutto);
                else
                    int.TryParse(txtTara.Text, out wpisywaneTara);
                
                UpdateBigDisplay();
            }
        }

        public void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TextBlock target = (aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara;
            target.Text = "0";
            
            // Wyczyść odpowiednią zmienną wpisywaną i wyłącz flagę
            if (aktywnePole == AktywnePole.Brutto)
            {
                czekaNaPierwszaCyfreBrutto = false;
                wpisywaneBrutto = 0;
            }
            else
            {
                czekaNaPierwszaCyfreTara = false;
                wpisywaneTara = 0;
            }
            
            UpdateBigDisplay();
        }

        public void BtnBackspace_Click(object sender, RoutedEventArgs e)
        {
            TextBlock target = (aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara;
            if (target.Text.Length > 0) target.Text = target.Text.Substring(0, target.Text.Length - 1);
            if (string.IsNullOrEmpty(target.Text)) target.Text = "0";
            
            // Wyłącz flagę i aktualizuj zmienną
            if (aktywnePole == AktywnePole.Brutto)
            {
                czekaNaPierwszaCyfreBrutto = false;
                int.TryParse(txtBrutto.Text, out wpisywaneBrutto);
            }
            else
            {
                czekaNaPierwszaCyfreTara = false;
                int.TryParse(txtTara.Text, out wpisywaneTara);
            }
            
            UpdateBigDisplay();
        }

        #endregion

        #region ZAPIS

        public void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            int brutto = 0, tara = 0;
            int.TryParse(txtBrutto.Text, out brutto);
            int.TryParse(txtTara.Text, out tara);

            if (brutto == 0 && tara == 0) return;

            bool success = false;

            if (aktualnyTryb == "Avilog")
            {
                if (WybranaDostwa == null) return;
                success = ZapiszAvilog(brutto, tara);
            }
            else
            {
                success = ZapiszOdpady(brutto, tara);
            }

            if (success)
            {
                PlaySound(true);
                if (selectedCardBorder != null)
                    AnimateSuccess(selectedCardBorder);
            }
            else
            {
                PlaySound(false);
            }

            LoadDostawy();
            ClearFormularz();
        }

        private bool ZapiszAvilog(int brutto, int tara)
        {
            int netto = (brutto > 0 && tara > 0) ? (brutto - tara) : 0;
            int prevB = originalBrutto;
            int prevT = originalTara;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"UPDATE dbo.FarmerCalc 
                                     SET FullFarmWeight=@B, EmptyFarmWeight=@T, NettoFarmWeight=@N,
                                         GodzinaTara = CASE WHEN @T > 0 AND (@PrevT = 0 OR @T <> @PrevT) THEN GETDATE() ELSE GodzinaTara END,
                                         GodzinaBrutto = CASE WHEN @B > 0 AND (@PrevB = 0 OR @B <> @PrevB) THEN GETDATE() ELSE GodzinaBrutto END
                                     WHERE ID=@ID";
                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@B", brutto);
                        cmd.Parameters.AddWithValue("@T", tara);
                        cmd.Parameters.AddWithValue("@N", netto);
                        cmd.Parameters.AddWithValue("@PrevB", prevB);
                        cmd.Parameters.AddWithValue("@PrevT", prevT);
                        cmd.Parameters.AddWithValue("@ID", WybranaDostwa.ID);
                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Blad zapisu: " + ex.Message, "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool ZapiszOdpady(int brutto, int tara)
        {
            int netto = (brutto > 0 && tara > 0) ? (brutto - tara) : 0;
            string status = (brutto > 0 && tara > 0) ? "Zakonczone" : "W toku";
            string odbiorca = (cbOdbiorcy.SelectedItem is Odbiorca o) ? o.Nazwa : "";
            string rejestracja = txtEditRejestracja.Text.Trim().ToUpper();
            int prevB = WybranaDostwa?.Brutto ?? 0;
            int prevT = WybranaDostwa?.Tara ?? 0;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query;

                    if (WybranaDostwa != null && WybranaDostwa.ID > 0)
                    {
                        query = @"UPDATE dbo.OdpadyRejestr 
                                  SET Brutto=@B, Tara=@T, Netto=@N, NrRejestracyjny=@Nr, 
                                      Odbiorca=@Odbiorca, Status=@Status, Towar=@Towar, Operator='Portier',
                                      GodzinaTara = CASE WHEN @T > 0 AND (@PrevT = 0 OR @T <> @PrevT) THEN GETDATE() ELSE GodzinaTara END,
                                      GodzinaBrutto = CASE WHEN @B > 0 AND (@PrevB = 0 OR @B <> @PrevB) THEN GETDATE() ELSE GodzinaBrutto END
                                  WHERE ID=@ID";
                    }
                    else
                    {
                        query = @"INSERT INTO dbo.OdpadyRejestr 
                                  (Towar, NrRejestracyjny, Odbiorca, DataWazenia, Brutto, Tara, Netto, Status, Operator, GodzinaTara, GodzinaBrutto) 
                                  VALUES (@Towar, @Nr, @Odbiorca, GETDATE(), @B, @T, @N, @Status, 'Portier',
                                          CASE WHEN @T > 0 THEN GETDATE() ELSE NULL END,
                                          CASE WHEN @B > 0 THEN GETDATE() ELSE NULL END)";
                    }

                    using (var cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@B", brutto);
                        cmd.Parameters.AddWithValue("@T", tara);
                        cmd.Parameters.AddWithValue("@N", netto);
                        cmd.Parameters.AddWithValue("@Nr", rejestracja);
                        cmd.Parameters.AddWithValue("@Towar", aktualnyTowar);
                        cmd.Parameters.AddWithValue("@Odbiorca", odbiorca);
                        cmd.Parameters.AddWithValue("@Status", status);
                        cmd.Parameters.AddWithValue("@PrevB", prevB);
                        cmd.Parameters.AddWithValue("@PrevT", prevT);

                        if (WybranaDostwa != null && WybranaDostwa.ID > 0)
                            cmd.Parameters.AddWithValue("@ID", WybranaDostwa.ID);

                        cmd.ExecuteNonQuery();
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Blad zapisu: " + ex.Message, "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        #endregion

        #region NOWY WPIS / USUWANIE

        public void BtnNewEntry_Click(object sender, RoutedEventArgs e)
        {
            ClearFormularz();
            WybranaDostwa = new DostawaPortiera { ID = 0, Towar = aktualnyTowar, Brutto = 0, Tara = 0, Netto = 0 };
            radioTara.IsChecked = true;
            aktywnePole = AktywnePole.Tara;
            UpdateBigDisplay();
        }

        public void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (WybranaDostwa == null || WybranaDostwa.ID <= 0) return;

            if (MessageBox.Show($"Czy na pewno usunac wpis?\n\n{WybranaDostwa.NrRejestracyjny} - {WybranaDostwa.Towar}",
                "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("DELETE FROM dbo.OdpadyRejestr WHERE ID=@ID AND CAST(DataWazenia AS DATE) = CAST(GETDATE() AS DATE)", conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", WybranaDostwa.ID);
                        cmd.ExecuteNonQuery();
                    }
                }
                PlaySound(true);
                LoadDostawy();
                ClearFormularz();
            }
            catch (Exception ex)
            {
                PlaySound(false);
                MessageBox.Show("Blad usuwania: " + ex.Message, "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region SKANOWANIE

        public void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (WybranaDostwa == null || WybranaDostwa.ID <= 0)
            {
                PlaySound(false);
                MessageBox.Show("Najpierw wybierz wpis z listy!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ofd = new OpenFileDialog
            {
                Title = "Wybierz zeskanowany dokument",
                Filter = "Obrazy i PDF|*.jpg;*.jpeg;*.png;*.pdf;*.bmp|Wszystkie pliki|*.*"
            };

            if (ofd.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(ofd.FileName);
                    string baseDir = @"C:\Skany\";
                    if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);

                    string subDir = aktualnyTryb == "Avilog" ? "Avilog" : "Odpady";
                    string targetDir = Path.Combine(baseDir, subDir, DateTime.Today.ToString("yyyy-MM-dd"));
                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    string fileName = $"{WybranaDostwa.ID}_{WybranaDostwa.NrRejestracyjny?.Replace(" ", "_")}_{DateTime.Now:HHmmss}{ext}";
                    string targetPath = Path.Combine(targetDir, fileName);

                    File.Copy(ofd.FileName, targetPath, true);
                    PlaySound(true);
                    MessageBox.Show($"Dokument zapisany:\n{targetPath}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    PlaySound(false);
                    MessageBox.Show("Blad zapisu skanu: " + ex.Message, "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region DRUKOWANIE WZ

        public void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (WybranaDostwa == null || WybranaDostwa.ID <= 0)
            {
                PlaySound(false);
                MessageBox.Show("Najpierw wybierz wpis z listy!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (WybranaDostwa.Brutto <= 0 || WybranaDostwa.Tara <= 0)
            {
                PlaySound(false);
                MessageBox.Show("Brak pelnych danych wagi (TARA i BRUTTO)!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string nrWz = $"WZ/{DateTime.Now:yyyy}/{nextWzNumber:D4}";
                int netto = WybranaDostwa.Brutto - WybranaDostwa.Tara;
                string towar = aktualnyTryb == "Avilog" ? "ŻYWIEC DROBIOWY" : WybranaDostwa.Towar;
                string odbiorca = aktualnyTryb == "Avilog" ? WybranaDostwa.HodowcaNazwa : (cbOdbiorcy.SelectedItem as Odbiorca)?.Nazwa ?? "";
                string rej = WybranaDostwa.NrRejestracyjny ?? $"{WybranaDostwa.CarID} {WybranaDostwa.TrailerID}";
                string kierowca = WybranaDostwa.KierowcaNazwa ?? "";

                var pd = new PrintDialog();
                if (pd.ShowDialog() == true)
                {
                    for (int kopia = 1; kopia <= 2; kopia++)
                    {
                        string kopiaText = kopia == 1 ? "ORYGINAŁ - AVILOG" : "KOPIA - UBOJNIA";

                        var doc = new FlowDocument
                        {
                            FontFamily = new FontFamily("Segoe UI"),
                            FontSize = 11,
                            PageWidth = pd.PrintableAreaWidth,
                            PageHeight = pd.PrintableAreaHeight,
                            PagePadding = new Thickness(40),
                            ColumnWidth = double.PositiveInfinity
                        };

                        var header = new Paragraph { TextAlignment = TextAlignment.Center };
                        header.Inlines.Add(new Run($"{FIRMA_NAZWA}\n") { FontWeight = FontWeights.Bold, FontSize = 18 });
                        header.Inlines.Add(new Run($"{FIRMA_ADRES}, {FIRMA_MIASTO}\n") { FontSize = 11 });
                        header.Inlines.Add(new Run($"NIP: {FIRMA_NIP}   REGON: {FIRMA_REGON}   Tel: {FIRMA_TEL}\n\n") { FontSize = 9, Foreground = Brushes.Gray });
                        header.Inlines.Add(new Run("═══════════════════════════════════════════\n") { FontSize = 10 });
                        header.Inlines.Add(new Run("DOKUMENT PRZEWOZOWY - WZ\n") { FontWeight = FontWeights.Bold, FontSize = 18 });
                        header.Inlines.Add(new Run($"Nr: {nrWz}\n") { FontSize = 14, FontWeight = FontWeights.Bold });
                        header.Inlines.Add(new Run($"Data wystawienia: {DateTime.Now:dd.MM.yyyy HH:mm}\n") { FontSize = 11 });
                        header.Inlines.Add(new Run($"[{kopiaText}]\n") { FontSize = 10, FontStyle = FontStyles.Italic });
                        header.Inlines.Add(new Run("═══════════════════════════════════════════\n\n") { FontSize = 10 });
                        doc.Blocks.Add(header);

                        var data = new Paragraph { LineHeight = 24 };
                        data.Inlines.Add(new Run("TOWAR: ") { FontWeight = FontWeights.Bold });
                        data.Inlines.Add(new Run($"{towar}\n") { FontSize = 14, FontWeight = FontWeights.Bold });
                        data.Inlines.Add(new Run("ODBIORCA/DOSTAWCA: ") { FontWeight = FontWeights.Bold });
                        data.Inlines.Add(new Run($"{odbiorca}\n") { FontSize = 13 });
                        data.Inlines.Add(new Run("NR REJESTRACYJNY: ") { FontWeight = FontWeights.Bold });
                        data.Inlines.Add(new Run($"{rej}\n") { FontSize = 14, FontWeight = FontWeights.Bold });

                        if (!string.IsNullOrEmpty(kierowca))
                        {
                            data.Inlines.Add(new Run("KIEROWCA: ") { FontWeight = FontWeights.Bold });
                            data.Inlines.Add(new Run($"{kierowca}\n") { FontSize = 13 });
                        }

                        data.Inlines.Add(new Run("\n───────────────────────────────────────────\n") { FontSize = 10 });
                        doc.Blocks.Add(data);

                        var weights = new Paragraph { LineHeight = 26 };
                        weights.Inlines.Add(new Run($"Godzina przyjazdu:          {WybranaDostwa.GodzinaPrzyjazdu}\n") { FontSize = 12 });
                        weights.Inlines.Add(new Run($"Godzina ważenia TARA:       {WybranaDostwa.GodzinaTaraDisplay}\n") { FontSize = 12 });
                        weights.Inlines.Add(new Run($"Godzina ważenia BRUTTO:     {WybranaDostwa.GodzinaBruttoDisplay}\n\n") { FontSize = 12 });

                        var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                        nfi.NumberGroupSeparator = " ";

                        weights.Inlines.Add(new Run($"WAGA TARA (puste):          {WybranaDostwa.Tara.ToString("N0", nfi)} kg\n") { FontSize = 14 });
                        weights.Inlines.Add(new Run($"WAGA BRUTTO (pelne):        {WybranaDostwa.Brutto.ToString("N0", nfi)} kg\n") { FontSize = 14 });
                        weights.Inlines.Add(new Run("═══════════════════════════════════════════\n") { FontSize = 10 });
                        weights.Inlines.Add(new Run($"WAGA NETTO:                 {netto.ToString("N0", nfi)} kg\n") { FontWeight = FontWeights.Bold, FontSize = 20 });
                        weights.Inlines.Add(new Run("═══════════════════════════════════════════\n\n\n") { FontSize = 10 });
                        doc.Blocks.Add(weights);

                        var signatures = new Paragraph { LineHeight = 30 };
                        signatures.Inlines.Add(new Run("Podpis kierowcy: _________________________\n\n\n"));
                        signatures.Inlines.Add(new Run("Podpis portiera: _________________________\n\n"));
                        signatures.Inlines.Add(new Run($"\n\nWydrukowano: {DateTime.Now:dd.MM.yyyy HH:mm:ss}") { FontSize = 8, Foreground = Brushes.Gray });
                        doc.Blocks.Add(signatures);

                        pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"WZ {nrWz} - {kopiaText}");
                    }

                    try
                    {
                        using (var conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            using (var cmd = new SqlCommand(@"IF OBJECT_ID('dbo.DokumentyWZ', 'U') IS NOT NULL 
                                INSERT INTO dbo.DokumentyWZ (NrWZ, DataWystawienia, IDRekordu, Tryb, Towar, Netto) 
                                VALUES (@NrWZ, GETDATE(), @ID, @Tryb, @Towar, @Netto)", conn))
                            {
                                cmd.Parameters.AddWithValue("@NrWZ", nextWzNumber);
                                cmd.Parameters.AddWithValue("@ID", WybranaDostwa.ID);
                                cmd.Parameters.AddWithValue("@Tryb", aktualnyTryb);
                                cmd.Parameters.AddWithValue("@Towar", towar);
                                cmd.Parameters.AddWithValue("@Netto", netto);
                                cmd.ExecuteNonQuery();
                            }
                        }
                    }
                    catch { }

                    nextWzNumber++;
                    PlaySound(true);
                    MessageBox.Show($"Wydrukowano dokument: {nrWz}\n(2 kopie: AVILOG + UBOJNIA)", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                PlaySound(false);
                MessageBox.Show("Blad drukowania: " + ex.Message, "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region WAGA RS232

        private void ConnectToScale(string portName, int baudRate)
        {
            try
            {
                if (!SerialPort.GetPortNames().Contains(portName)) return;

                serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                serialPort.DataReceived += (s, e) =>
                {
                    try
                    {
                        string data = serialPort.ReadLine();
                        Dispatcher.Invoke(() =>
                        {
                            var match = Regex.Match(data, @"(\d+)");
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int weight))
                            {
                                TextBlock target = (aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara;
                                target.Text = weight.ToString();
                                UpdateBigDisplay();
                            }
                        });
                    }
                    catch { }
                };
                serialPort.Open();
                ledStabilnosc.Fill = Brushes.Green;
            }
            catch
            {
                ledStabilnosc.Fill = Brushes.Gray;
            }
        }

        public void BtnReadScale_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                try { serialPort.WriteLine("R"); }
                catch { }
            }
        }

        #endregion
    }

    public class DostawaPortiera : INotifyPropertyChanged
    {
        public long ID { get; set; }
        public string Lp { get; set; }
        public string GodzinaPrzyjazdu { get; set; }
        public string HodowcaNazwa { get; set; }
        public string KierowcaNazwa { get; set; }
        public string CarID { get; set; }
        public string TrailerID { get; set; }
        public string NrRejestracyjny { get; set; }
        public string Towar { get; set; }
        public int SztukiPlan { get; set; }
        public string GodzinaTaraDisplay { get; set; } = "-";
        public string GodzinaBruttoDisplay { get; set; } = "-";

        private int _brutto;
        public int Brutto
        {
            get => _brutto;
            set
            {
                _brutto = value;
                OnPropertyChanged(nameof(Brutto));
                OnPropertyChanged(nameof(BruttoDisplay));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(CardBorderBrush));
            }
        }

        private int _tara;
        public int Tara
        {
            get => _tara;
            set
            {
                _tara = value;
                OnPropertyChanged(nameof(Tara));
                OnPropertyChanged(nameof(TaraDisplay));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(CardBorderBrush));
            }
        }

        private int _netto;
        public int Netto
        {
            get => _netto;
            set
            {
                _netto = value;
                OnPropertyChanged(nameof(Netto));
                OnPropertyChanged(nameof(NettoDisplay));
            }
        }

        public string BruttoDisplay => Brutto > 0 ? $"{Brutto:N0} kg".Replace(",", " ") : "-";
        public string TaraDisplay => Tara > 0 ? $"{Tara:N0} kg".Replace(",", " ") : "-";
        public string NettoDisplay => (Brutto > 0 && Tara > 0) ? $"{(Brutto - Tara):N0} kg".Replace(",", " ") : "-";

        public SolidColorBrush StatusColor
        {
            get
            {
                if (Brutto > 0 && Tara > 0) return new SolidColorBrush(Colors.LimeGreen);
                if (Brutto > 0 || Tara > 0) return new SolidColorBrush(Colors.Orange);
                return new SolidColorBrush(Colors.Red);
            }
        }

        public SolidColorBrush CardBackground => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E2E"));

        public SolidColorBrush CardBorderBrush
        {
            get
            {
                if (Brutto > 0 && Tara > 0) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                if (Brutto > 0 || Tara > 0) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA726"));
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444"));
            }
        }

        public string TowarIcon
        {
            get
            {
                switch (Towar)
                {
                    case "KREW": return "\u2764";
                    case "LAPY": return "\U0001F43E";
                    case "PIORA": return "~";
                    case "JELITA": return "\u27B0";
                    case "ODPADY": return "\u267B";
                    default: return "\u2751";
                }
            }
        }

        public SolidColorBrush TowarColor
        {
            get
            {
                switch (Towar)
                {
                    case "KREW": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF5555"));
                    case "LAPY": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD54F"));
                    case "PIORA": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BDBDBD"));
                    case "JELITA": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A1887F"));
                    case "ODPADY": return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#AB47BC"));
                    default: return new SolidColorBrush(Colors.White);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}