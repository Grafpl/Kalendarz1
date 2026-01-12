using Microsoft.Data.SqlClient;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Media;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Printing;

// Aliasy dla rozwiązania konfliktu System.Drawing vs System.Windows.Media
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using Point = System.Windows.Point;
using Image = System.Windows.Controls.Image;

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
        private bool _waitingForScaleRead = false; // Flaga: czy czekamy na odczyt wagi (tylko po kliknięciu)
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

            ConnectToScale("COM1", 9600);
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

                // Aktualizuj towar w wybranej dostawie (zawsze, nie tylko dla nowych)
                if (WybranaDostwa != null)
                    WybranaDostwa.Towar = aktualnyTowar;
            }
        }

        #endregion

        #region KLAWIATURA EKRANOWA

        private void TxtEditRejestracja_Click(object sender, MouseButtonEventArgs e)
        {
            // Blokuj edycję gdy nie ma zaznaczenia
            if (WybranaDostwa == null)
            {
                PokazKomunikatNajpierwNowe();
                return;
            }
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

        private void TxtEditRejestracja_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Aktualizuj nr rejestracyjny w wybranej dostawie na bieżąco
            if (WybranaDostwa != null)
            {
                WybranaDostwa.NrRejestracyjny = txtEditRejestracja.Text.Trim().ToUpper();
            }
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
                            ISNULL(fc.FullWeight, 0) as Brutto, ISNULL(fc.EmptyWeight, 0) as Tara,
                            ISNULL(fc.NettoWeight, 0) as Netto, fc.Przyjazd, fc.GodzinaTara, fc.GodzinaBrutto,
                            fc.ZdjecieTaraPath, fc.ZdjecieBruttoPath
                        FROM dbo.FarmerCalc fc 
                        LEFT JOIN dbo.Driver dr ON fc.DriverGID = dr.GID
                        WHERE CAST(fc.CalcDate AS DATE) = @Data
                        ORDER BY fc.Przyjazd ASC, fc.ID";

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
                                        GodzinaBruttoDisplay = godzBrutto?.ToString("HH:mm") ?? "-",
                                        ZdjecieTaraPath = r["ZdjecieTaraPath"]?.ToString(),
                                        ZdjecieBruttoPath = r["ZdjecieBruttoPath"]?.ToString()
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
                                   ISNULL(Towar, 'KREW') as Towar, GodzinaTara, GodzinaBrutto,
                                   ZdjecieTaraPath, ZdjecieBruttoPath
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
                                        ZdjecieTaraPath = r["ZdjecieTaraPath"]?.ToString(),
                                        ZdjecieBruttoPath = r["ZdjecieBruttoPath"]?.ToString(),
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

        #region MENU KONTEKSTOWE - ZDJĘCIA

        private void Dostawa_RightClick(object sender, MouseButtonEventArgs e)
        {
            // Menu kontekstowe jest obsługiwane automatycznie przez ContextMenu
            e.Handled = false;
        }

        private void MenuZdjecieTara_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.Tag is DostawaPortiera dostawa)
            {
                string title = $"TARA - {dostawa.NrRejestracyjny} ({dostawa.TaraDisplay})";
                ShowWeightPhoto(dostawa.ZdjecieTaraPath, title);
            }
        }

        private void MenuZdjecieBrutto_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.Tag is DostawaPortiera dostawa)
            {
                string title = $"BRUTTO - {dostawa.NrRejestracyjny} ({dostawa.BruttoDisplay})";
                ShowWeightPhoto(dostawa.ZdjecieBruttoPath, title);
            }
        }

        private void MenuZdjecieTaraGrid_Click(object sender, RoutedEventArgs e)
        {
            if (gridTable.SelectedItem is DostawaPortiera dostawa)
            {
                string title = $"TARA - {dostawa.NrRejestracyjny} ({dostawa.TaraDisplay})";
                ShowWeightPhoto(dostawa.ZdjecieTaraPath, title);
            }
            else
            {
                MessageBox.Show("Najpierw zaznacz wiersz w tabeli.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void MenuZdjecieBruttoGrid_Click(object sender, RoutedEventArgs e)
        {
            if (gridTable.SelectedItem is DostawaPortiera dostawa)
            {
                string title = $"BRUTTO - {dostawa.NrRejestracyjny} ({dostawa.BruttoDisplay})";
                ShowWeightPhoto(dostawa.ZdjecieBruttoPath, title);
            }
            else
            {
                MessageBox.Show("Najpierw zaznacz wiersz w tabeli.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

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

        private void PokazKomunikatNajpierwNowe()
        {
            if (aktualnyTryb != "Avilog")
            {
                MessageBox.Show(
                    "Aby wprowadzić wagę:\n\n" +
                    "1. Wybierz towar (Krew, Łapy, Jelita, Pióra, Odpady)\n" +
                    "2. Wybierz odbiorcę z listy\n" +
                    "3. Naciśnij przycisk NOWE\n\n" +
                    "Dopiero wtedy możesz wpisać lub odczytać wagę.",
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void NumpadClick(object sender, RoutedEventArgs e)
        {
            // Blokuj edycję gdy nie ma zaznaczenia
            if (WybranaDostwa == null)
            {
                PokazKomunikatNajpierwNowe();
                return;
            }

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
            // Blokuj gdy nie ma zaznaczenia
            if (WybranaDostwa == null)
            {
                PokazKomunikatNajpierwNowe();
                return;
            }

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
            // Blokuj gdy nie ma zaznaczenia
            if (WybranaDostwa == null)
            {
                PokazKomunikatNajpierwNowe();
                return;
            }

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

            // Pozwól zapisać jeśli jest jakakolwiek zmiana (nawet zerowanie)
            bool jestZmiana = (brutto != originalBrutto) || (tara != originalTara);
            if (!jestZmiana && brutto == 0 && tara == 0) return;

            bool success = false;

            if (aktualnyTryb == "Avilog")
            {
                if (WybranaDostwa == null) return;
                success = ZapiszAvilog(brutto, tara);
            }
            else
            {
                // W trybie ODPADY wymagaj zaznaczenia lub nowego wpisu
                if (WybranaDostwa == null)
                {
                    PokazKomunikatNajpierwNowe();
                    return;
                }
                success = ZapiszOdpady(brutto, tara);
            }

            if (success)
            {
                PlaySound(true);
                if (selectedCardBorder != null)
                    AnimateSuccess(selectedCardBorder);

                // Drukuj paragon
                string typ = (tara > 0 && brutto == 0) ? "TARA" : (brutto > 0 ? "BRUTTO" : "");
                if (!string.IsNullOrEmpty(typ))
                {
                    if (aktualnyTryb == "Avilog")
                    {
                        string rej = $"{WybranaDostwa?.CarID} {WybranaDostwa?.TrailerID}";
                        int netto = (brutto > 0 && tara > 0) ? (brutto - tara) : 0;
                        PrintReceipt(rej, "ŻYWIEC", WybranaDostwa?.HodowcaNazwa ?? "", brutto, tara, netto, typ);
                    }
                    else
                    {
                        string rej = txtEditRejestracja.Text.Trim();
                        string odbNazwa = (cbOdbiorcy.SelectedItem is Odbiorca o) ? o.Nazwa : "";
                        int netto = (brutto > 0 && tara > 0) ? (brutto - tara) : 0;
                        PrintReceipt(rej, aktualnyTowar, odbNazwa, brutto, tara, netto, typ);
                    }
                }
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
            
            string rejestracja = $"{WybranaDostwa?.CarID}_{WybranaDostwa?.TrailerID}";
            long dostawaId = WybranaDostwa.ID;
            
            Debug.WriteLine($"[ZAPIS AVILOG] ID={dostawaId}, Rej={rejestracja}");
            Debug.WriteLine($"[ZAPIS AVILOG] Tara={tara} (prev={prevT}), Brutto={brutto} (prev={prevB})");
            
            // Zapisz zdjęcia w tle - ZAWSZE gdy wartość > 0 (nadpisuje poprzednie)
            if (tara > 0)
            {
                Debug.WriteLine($"[ZAPIS AVILOG] Robię zdjęcie TARA...");
                Task.Run(async () =>
                {
                    try
                    {
                        Debug.WriteLine($"[FOTO TARA] Start pobierania...");
                        var path = await SaveCameraSnapshot("AVILOG", rejestracja, "TARA");
                        Debug.WriteLine($"[FOTO TARA] Ścieżka: {path ?? "NULL"}");
                        if (!string.IsNullOrEmpty(path))
                        {
                            await UpdatePhotoPathInDb("FarmerCalc", dostawaId, "ZdjecieTaraPath", path);
                            Debug.WriteLine($"[FOTO TARA] Zapisano do bazy");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[FOTO TARA] BŁĄD: {ex.Message}");
                    }
                });
            }
            
            if (brutto > 0)
            {
                Debug.WriteLine($"[ZAPIS AVILOG] Robię zdjęcie BRUTTO...");
                Task.Run(async () =>
                {
                    try
                    {
                        Debug.WriteLine($"[FOTO BRUTTO] Start pobierania...");
                        var path = await SaveCameraSnapshot("AVILOG", rejestracja, "BRUTTO");
                        Debug.WriteLine($"[FOTO BRUTTO] Ścieżka: {path ?? "NULL"}");
                        if (!string.IsNullOrEmpty(path))
                        {
                            await UpdatePhotoPathInDb("FarmerCalc", dostawaId, "ZdjecieBruttoPath", path);
                            Debug.WriteLine($"[FOTO BRUTTO] Zapisano do bazy");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[FOTO BRUTTO] BŁĄD: {ex.Message}");
                    }
                });
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"UPDATE dbo.FarmerCalc
                                     SET FullWeight=@B, EmptyWeight=@T, NettoWeight=@N,
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
                        cmd.Parameters.AddWithValue("@ID", dostawaId);
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
            string towar = aktualnyTowar;
            long dostawaId = WybranaDostwa?.ID ?? 0;
            
            Debug.WriteLine($"[ZAPIS ODPADY] ID={dostawaId}, Rej={rejestracja}, Towar={towar}");
            Debug.WriteLine($"[ZAPIS ODPADY] Tara={tara} (prev={prevT}), Brutto={brutto} (prev={prevB})");

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query;
                    long insertedId = dostawaId;

                    if (WybranaDostwa != null && WybranaDostwa.ID > 0)
                    {
                        query = @"UPDATE dbo.OdpadyRejestr 
                                  SET Brutto=@B, Tara=@T, Netto=@N, NrRejestracyjny=@Nr, 
                                      Odbiorca=@Odbiorca, Status=@Status, Towar=@Towar, Operator='Portier',
                                      GodzinaTara = CASE WHEN @T > 0 AND (@PrevT = 0 OR @T <> @PrevT) THEN GETDATE() ELSE GodzinaTara END,
                                      GodzinaBrutto = CASE WHEN @B > 0 AND (@PrevB = 0 OR @B <> @PrevB) THEN GETDATE() ELSE GodzinaBrutto END
                                  WHERE ID=@ID";
                        
                        using (var cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@B", brutto);
                            cmd.Parameters.AddWithValue("@T", tara);
                            cmd.Parameters.AddWithValue("@N", netto);
                            cmd.Parameters.AddWithValue("@Nr", rejestracja);
                            cmd.Parameters.AddWithValue("@Towar", towar);
                            cmd.Parameters.AddWithValue("@Odbiorca", odbiorca);
                            cmd.Parameters.AddWithValue("@Status", status);
                            cmd.Parameters.AddWithValue("@PrevB", prevB);
                            cmd.Parameters.AddWithValue("@PrevT", prevT);
                            cmd.Parameters.AddWithValue("@ID", dostawaId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        query = @"INSERT INTO dbo.OdpadyRejestr 
                                  (Towar, NrRejestracyjny, Odbiorca, DataWazenia, Brutto, Tara, Netto, Status, Operator, GodzinaTara, GodzinaBrutto) 
                                  VALUES (@Towar, @Nr, @Odbiorca, GETDATE(), @B, @T, @N, @Status, 'Portier',
                                          CASE WHEN @T > 0 THEN GETDATE() ELSE NULL END,
                                          CASE WHEN @B > 0 THEN GETDATE() ELSE NULL END);
                                  SELECT SCOPE_IDENTITY();";
                        
                        using (var cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@B", brutto);
                            cmd.Parameters.AddWithValue("@T", tara);
                            cmd.Parameters.AddWithValue("@N", netto);
                            cmd.Parameters.AddWithValue("@Nr", rejestracja);
                            cmd.Parameters.AddWithValue("@Towar", towar);
                            cmd.Parameters.AddWithValue("@Odbiorca", odbiorca);
                            cmd.Parameters.AddWithValue("@Status", status);
                            
                            var result = cmd.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                insertedId = Convert.ToInt64(result);
                                // WAŻNE: Zaktualizuj ID w wybranej dostawie aby kolejny zapis był UPDATE nie INSERT
                                if (WybranaDostwa != null)
                                {
                                    WybranaDostwa.ID = insertedId;
                                    Debug.WriteLine($"[ZAPIS ODPADY] Zaktualizowano WybranaDostwa.ID = {insertedId}");
                                }
                            }

                            Debug.WriteLine($"[ZAPIS ODPADY] Nowy ID po INSERT: {insertedId}");
                        }
                    }
                    
                    // Zapisz zdjęcia w tle - ZAWSZE gdy wartość > 0 (nadpisuje poprzednie)
                    if (tara > 0 && insertedId > 0)
                    {
                        Debug.WriteLine($"[ZAPIS ODPADY] Robię zdjęcie TARA...");
                        Task.Run(async () =>
                        {
                            try
                            {
                                var path = await SaveCameraSnapshot("ODPADY", rejestracja, "TARA", towar);
                                Debug.WriteLine($"[FOTO ODPADY TARA] Ścieżka: {path ?? "NULL"}");
                                if (!string.IsNullOrEmpty(path))
                                {
                                    await UpdatePhotoPathInDb("OdpadyRejestr", insertedId, "ZdjecieTaraPath", path);
                                    Debug.WriteLine($"[FOTO ODPADY TARA] Zapisano do bazy");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[FOTO ODPADY TARA] BŁĄD: {ex.Message}");
                            }
                        });
                    }
                    
                    if (brutto > 0 && insertedId > 0)
                    {
                        Debug.WriteLine($"[ZAPIS ODPADY] Robię zdjęcie BRUTTO...");
                        Task.Run(async () =>
                        {
                            try
                            {
                                var path = await SaveCameraSnapshot("ODPADY", rejestracja, "BRUTTO", towar);
                                Debug.WriteLine($"[FOTO ODPADY BRUTTO] Ścieżka: {path ?? "NULL"}");
                                if (!string.IsNullOrEmpty(path))
                                {
                                    await UpdatePhotoPathInDb("OdpadyRejestr", insertedId, "ZdjecieBruttoPath", path);
                                    Debug.WriteLine($"[FOTO ODPADY BRUTTO] Zapisano do bazy");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[FOTO ODPADY BRUTTO] BŁĄD: {ex.Message}");
                            }
                        });
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
        
        /// <summary>
        /// Aktualizuje ścieżkę zdjęcia w bazie (wywoływane asynchronicznie w tle)
        /// </summary>
        private async Task UpdatePhotoPathInDb(string tableName, long id, string columnName, string path)
        {
            try
            {
                await Task.Run(() =>
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = $"UPDATE dbo.{tableName} SET {columnName} = @Path WHERE ID = @ID";
                        using (var cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Path", path);
                            cmd.Parameters.AddWithValue("@ID", id);
                            cmd.ExecuteNonQuery();
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FOTO] Błąd zapisu ścieżki do bazy: {ex.Message}");
            }
        }

        #endregion

        #region NOWY WPIS / USUWANIE

        public void BtnNewEntry_Click(object sender, RoutedEventArgs e)
        {
            ClearFormularz();

            // Utwórz nowy wpis z bieżącą godziną
            var nowyWpis = new DostawaPortiera
            {
                ID = 0,
                Towar = aktualnyTowar,
                Brutto = 0,
                Tara = 0,
                Netto = 0,
                GodzinaPrzyjazdu = DateTime.Now.ToString("HH:mm"),
                NrRejestracyjny = "",
                HodowcaNazwa = "(nowy wpis)"
            };

            // Dodaj do listy aby był widoczny w tabeli
            dostawy.Insert(0, nowyWpis);

            // Zaznacz nowy wiersz
            WybranaDostwa = nowyWpis;
            gridTable.SelectedItem = nowyWpis;

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

        #region DRUKOWANIE WAGI (PICCO)

        public void BtnDrukujWage_Click(object sender, RoutedEventArgs e)
        {
            if (WybranaDostwa == null || WybranaDostwa.ID <= 0)
            {
                PlaySound(false);
                MessageBox.Show("Najpierw wybierz wpis z listy!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (WybranaDostwa.Brutto <= 0 && WybranaDostwa.Tara <= 0)
            {
                PlaySound(false);
                MessageBox.Show("Brak danych wagi do wydruku!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int netto = WybranaDostwa.Brutto - WybranaDostwa.Tara;
                string hodowca = WybranaDostwa.HodowcaNazwa ?? "---";
                string kierowca = WybranaDostwa.KierowcaNazwa ?? "---";
                string pojazd = WybranaDostwa.NrRejestracyjny ?? $"{WybranaDostwa.CarID} {WybranaDostwa.TrailerID}".Trim();

                // Szukaj drukarki PICCO
                string piccoPrinter = null;
                foreach (string printerName in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
                {
                    if (printerName.ToUpper().Contains("PICCO"))
                    {
                        piccoPrinter = printerName;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(piccoPrinter))
                {
                    PlaySound(false);
                    MessageBox.Show("Nie znaleziono drukarki PICCO!\n\nSprawdź czy drukarka jest podłączona i zainstalowana.",
                        "Brak drukarki", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var pd = new PrintDialog();
                pd.PrintQueue = new LocalPrintServer().GetPrintQueue(piccoPrinter);

                // Tworzenie dokumentu dla drukarki termicznej (wąski paragon)
                var doc = new FlowDocument
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 10,
                    PageWidth = 200, // ~58mm dla drukarki PICCO
                    PagePadding = new Thickness(5),
                    ColumnWidth = double.PositiveInfinity
                };

                // Nagłówek
                var header = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 0, 0, 5) };
                header.Inlines.Add(new Run("PRONOVA Sp. z o.o.\n") { FontWeight = FontWeights.Bold, FontSize = 11 });
                header.Inlines.Add(new Run("================================\n") { FontSize = 8 });
                header.Inlines.Add(new Run("KWIT WAGOWY\n") { FontWeight = FontWeights.Bold, FontSize = 12 });
                header.Inlines.Add(new Run($"{DateTime.Now:dd.MM.yyyy HH:mm}\n") { FontSize = 9 });
                header.Inlines.Add(new Run("================================\n") { FontSize = 8 });
                doc.Blocks.Add(header);

                // Dane
                var data = new Paragraph { Margin = new Thickness(0, 5, 0, 5), LineHeight = 16 };
                data.Inlines.Add(new Run($"Hodowca:\n") { FontSize = 8 });
                data.Inlines.Add(new Run($"{hodowca}\n") { FontWeight = FontWeights.Bold, FontSize = 10 });
                data.Inlines.Add(new Run($"\nKierowca:\n") { FontSize = 8 });
                data.Inlines.Add(new Run($"{kierowca}\n") { FontSize = 9 });
                data.Inlines.Add(new Run($"\nPojazd:\n") { FontSize = 8 });
                data.Inlines.Add(new Run($"{pojazd}\n") { FontWeight = FontWeights.Bold, FontSize = 10 });
                doc.Blocks.Add(data);

                // Wagi
                var weights = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 5, 0, 5) };
                weights.Inlines.Add(new Run("--------------------------------\n") { FontSize = 8 });
                weights.Inlines.Add(new Run($"BRUTTO: {WybranaDostwa.Brutto} kg\n") { FontSize = 11 });
                weights.Inlines.Add(new Run($"TARA:   {WybranaDostwa.Tara} kg\n") { FontSize = 11 });
                weights.Inlines.Add(new Run("--------------------------------\n") { FontSize = 8 });
                weights.Inlines.Add(new Run($"NETTO:  {netto} kg\n") { FontWeight = FontWeights.Bold, FontSize = 14 });
                weights.Inlines.Add(new Run("================================\n") { FontSize = 8 });
                doc.Blocks.Add(weights);

                // Stopka
                var footer = new Paragraph { TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };
                footer.Inlines.Add(new Run($"LP: {WybranaDostwa.Lp}  ID: {WybranaDostwa.ID}\n") { FontSize = 8 });
                footer.Inlines.Add(new Run("\n\n\n") { FontSize = 6 }); // Miejsce na odcięcie
                doc.Blocks.Add(footer);

                pd.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, $"Kwit wagowy {WybranaDostwa.ID}");

                PlaySound(true);
                // Kwit wydrukowany - dźwięk potwierdza sukces
            }
            catch (Exception ex)
            {
                PlaySound(false);
                MessageBox.Show($"Błąd drukowania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region WAGA RS232

        private void ConnectToScale(string portName, int baudRate)
        {
            try
            {
                var availablePorts = SerialPort.GetPortNames();
                System.Diagnostics.Debug.WriteLine($"[WAGA] Dostępne porty: {string.Join(", ", availablePorts)}");
                
                if (!availablePorts.Contains(portName))
                {
                    System.Diagnostics.Debug.WriteLine($"[WAGA] Port {portName} nie znaleziony!");
                    ledStabilnosc.Fill = Brushes.Red;
                    return;
                }

                serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                serialPort.ReadTimeout = 2000;
                serialPort.WriteTimeout = 1000;
                serialPort.NewLine = "\r\n";
                serialPort.Handshake = Handshake.None;
                serialPort.DtrEnable = true;
                serialPort.RtsEnable = true;
                
                serialPort.DataReceived += (s, e) =>
                {
                    try
                    {
                        // Ignoruj dane jeśli nie czekamy na odczyt (użytkownik nie kliknął w wyświetlacz)
                        if (!_waitingForScaleRead)
                        {
                            serialPort.DiscardInBuffer(); // Wyczyść bufor
                            return;
                        }

                        System.Threading.Thread.Sleep(100); // Poczekaj na pełne dane
                        string data = serialPort.ReadExisting();
                        System.Diagnostics.Debug.WriteLine($"[WAGA] Odebrano RAW: '{data}' (hex: {BitConverter.ToString(System.Text.Encoding.ASCII.GetBytes(data))})");
                        
                        Dispatcher.Invoke(() =>
                        {
                            // Parsuj wagę - szukaj liczby (może być ze spacjami, znakiem, jednostką)
                            // Formaty RHEWA: "+  12345 kg", "12345", "  12345  ", "S +    12345 kg"
                            var cleanData = data.Replace("kg", "").Replace("KG", "").Replace("+", "").Replace("-", "").Trim();
                            var match = Regex.Match(cleanData, @"(\d+)");
                            
                            if (match.Success && int.TryParse(match.Groups[1].Value, out int weight))
                            {
                                System.Diagnostics.Debug.WriteLine($"[WAGA] Parsowana waga: {weight} kg");
                                TextBlock target = (aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara;
                                target.Text = weight.ToString();
                                
                                // Aktualizuj zmienne
                                if (aktywnePole == AktywnePole.Brutto)
                                {
                                    wpisywaneBrutto = weight;
                                    czekaNaPierwszaCyfreBrutto = false;
                                }
                                else
                                {
                                    wpisywaneTara = weight;
                                    czekaNaPierwszaCyfreTara = false;
                                }
                                
                                UpdateBigDisplay();
                                ledStabilnosc.Fill = Brushes.LimeGreen;

                                // Zakończ oczekiwanie na odczyt
                                _waitingForScaleRead = false;
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[WAGA] Nie znaleziono liczby w: '{data}'");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[WAGA] Błąd odczytu: {ex.Message}");
                    }
                };
                
                serialPort.Open();
                ledStabilnosc.Fill = Brushes.Green;
                System.Diagnostics.Debug.WriteLine($"[WAGA] Połączono z {portName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WAGA] Błąd połączenia: {ex.Message}");
                ledStabilnosc.Fill = Brushes.Gray;
            }
        }

        public void BigDisplay_Click(object sender, MouseButtonEventArgs e)
        {
            // Sprawdź czy jest wybrana dostawa
            if (WybranaDostwa == null)
            {
                PokazKomunikatNajpierwNowe();
                return;
            }
            
            // Sprawdź dostępne porty COM
            var availablePorts = SerialPort.GetPortNames();
            if (availablePorts.Length == 0)
            {
                ledStabilnosc.Fill = Brushes.Red;
                MessageBox.Show("Nie wykryto żadnego portu COM.\n\nSprawdź:\n• Czy kabel RS232/USB jest podłączony\n• Czy sterowniki są zainstalowane", 
                    "Brak portu COM", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            // Jeśli port nie istnieje - próbuj połączyć
            if (serialPort == null)
            {
                string portToUse = availablePorts.Contains("COM1") ? "COM1" : availablePorts[0];
                ConnectToScale(portToUse, 9600);
                
                if (serialPort == null || !serialPort.IsOpen)
                {
                    ledStabilnosc.Fill = Brushes.Red;
                    MessageBox.Show($"Nie można połączyć z wagą.\n\nDostępne porty: {string.Join(", ", availablePorts)}\n\nSprawdź:\n• Czy waga jest włączona\n• Czy kabel jest podłączony\n• Numer portu COM w Menedżerze urządzeń", 
                        "Błąd połączenia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            
            // Jeśli port zamknięty - otwórz
            if (!serialPort.IsOpen)
            {
                try
                {
                    serialPort.Open();
                    ledStabilnosc.Fill = Brushes.Green;
                }
                catch (UnauthorizedAccessException)
                {
                    ledStabilnosc.Fill = Brushes.Red;
                    MessageBox.Show($"Port {serialPort.PortName} jest używany przez inny program.\n\nZamknij inne programy korzystające z wagi.", 
                        "Port zajęty", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                catch (Exception ex)
                {
                    ledStabilnosc.Fill = Brushes.Red;
                    MessageBox.Show($"Nie można otworzyć portu {serialPort.PortName}:\n{ex.Message}", 
                        "Błąd połączenia", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            
            // Wyślij komendę odczytu
            try
            {
                ledStabilnosc.Fill = Brushes.Yellow;
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();

                // Ustaw flagę - teraz czekamy na odczyt z wagi
                _waitingForScaleRead = true;

                // RHEWA 82c - próbujemy różne komendy
                // ENQ (ASCII 5) - standardowa komenda żądania odczytu
                serialPort.Write(new byte[] { 0x05 }, 0, 1); // ENQ
                System.Diagnostics.Debug.WriteLine("[WAGA] Wysłano ENQ (0x05) - czekam na odpowiedź...");

                // Timer sprawdzający czy przyszła odpowiedź
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    if (ledStabilnosc.Fill == Brushes.Yellow) // Nadal czeka - brak odpowiedzi
                    {
                        _waitingForScaleRead = false; // Anuluj oczekiwanie
                        ledStabilnosc.Fill = Brushes.Red;
                        MessageBox.Show("Waga nie odpowiada.\n\nSprawdź:\n• Czy waga jest włączona i stabilna\n• Czy ładunek jest na wadze\n• Ustawienia komunikacji wagi (9600 baud, 8N1)", 
                            "Brak odpowiedzi", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                _waitingForScaleRead = false; // Anuluj oczekiwanie
                ledStabilnosc.Fill = Brushes.Red;
                MessageBox.Show($"Błąd wysyłania komendy do wagi:\n{ex.Message}", 
                    "Błąd komunikacji", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void BtnReadScale_Click(object sender, RoutedEventArgs e)
        {
            BigDisplay_Click(sender, null);
        }

        #endregion

        #region KLIKNIĘCIE W PUSTE MIEJSCE

        private void GridTable_MouseClick(object sender, MouseButtonEventArgs e)
        {
            // Sprawdź czy kliknięto w pusty obszar (nie w wiersz)
            var hit = VisualTreeHelper.HitTest(gridTable, e.GetPosition(gridTable));
            if (hit != null)
            {
                var row = FindParent<DataGridRow>(hit.VisualHit);
                if (row == null)
                {
                    // Kliknięto w puste miejsce - odznacz
                    gridTable.SelectedItem = null;
                    ClearFormularz();
                }
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }

        #endregion

        #region KAMERA

        private const string CAMERA_IP = "192.168.0.76";
        private const string CAMERA_USER = "admin";
        private const string CAMERA_PASS = "terePacja12$";
        private DispatcherTimer cameraTimer;
        private bool cameraActive = false;
        
        // Zoom i przesuwanie kamery
        private double currentZoom = 1.0;
        private Point cameraDragStart;
        private bool isDraggingCamera = false;

        public void BtnCamera_Click(object sender, RoutedEventArgs e)
        {
            // Normalny tryb - nie wpisuj do TextBox
            cameraScanToTextBoxMode = false;
            
            CameraOverlay.Visibility = Visibility.Visible;
            cameraStatus.Text = "Łączenie z kamerą...";
            cameraImage.Source = null;
            
            // Reset zoom przy otwarciu
            ResetCameraZoom();
            
            StartCameraStream();
        }

        private void CameraClose_Click(object sender, RoutedEventArgs e)
        {
            StopCameraStream();
            CameraOverlay.Visibility = Visibility.Collapsed;
            cameraScanToTextBoxMode = false; // Reset trybu
        }

        private void CameraOverlay_Click(object sender, MouseButtonEventArgs e)
        {
            // Zamknij tylko jeśli kliknięto w tło (nie w panel kamery)
            if (e.OriginalSource == CameraOverlay || e.OriginalSource is Grid g && g.Name == "")
            {
                StopCameraStream();
                CameraOverlay.Visibility = Visibility.Collapsed;
                cameraScanToTextBoxMode = false; // Reset trybu
            }
        }

        private void CameraPanel_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Nie zamykaj gdy kliknięto w panel
        }
        
        #region ZOOM KAMERY
        
        private void ResetCameraZoom()
        {
            currentZoom = 1.0;
            cameraScale.ScaleX = 1.0;
            cameraScale.ScaleY = 1.0;
            cameraTranslate.X = 0;
            cameraTranslate.Y = 0;
            UpdateZoomLabel();
        }
        
        private void UpdateZoomLabel()
        {
            if (txtZoomLevel != null)
                txtZoomLevel.Text = $"{(int)(currentZoom * 100)}%";
        }
        
        private void SetZoom(double zoom)
        {
            currentZoom = Math.Max(0.25, Math.Min(8.0, zoom)); // Min 25%, Max 800%
            cameraScale.ScaleX = currentZoom;
            cameraScale.ScaleY = currentZoom;
            
            // Jeśli zoom < 1, wycentruj
            if (currentZoom <= 1.0)
            {
                cameraTranslate.X = 0;
                cameraTranslate.Y = 0;
            }
            
            UpdateZoomLabel();
        }
        
        private void CameraZoomIn_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(currentZoom * 1.25);
        }
        
        private void CameraZoomOut_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(currentZoom / 1.25);
        }
        
        private void CameraZoomReset_Click(object sender, RoutedEventArgs e)
        {
            ResetCameraZoom();
        }
        
        private void CameraZoom50_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(0.5);
        }
        
        private void CameraZoom100_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(1.0);
            cameraTranslate.X = 0;
            cameraTranslate.Y = 0;
        }
        
        private void CameraZoom200_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(2.0);
        }
        
        private void CameraZoom400_Click(object sender, RoutedEventArgs e)
        {
            SetZoom(4.0);
        }
        
        private void CameraZoom_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = e.Delta > 0 ? 1.15 : 1 / 1.15;
            SetZoom(currentZoom * zoomFactor);
            e.Handled = true;
        }
        
        private void CameraImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (currentZoom > 1.0)
            {
                isDraggingCamera = true;
                cameraDragStart = e.GetPosition(cameraContainer);
                cameraContainer.CaptureMouse();
                cameraContainer.Cursor = Cursors.ScrollAll;
            }
            e.Handled = true;
        }
        
        private void CameraImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (isDraggingCamera)
            {
                isDraggingCamera = false;
                cameraContainer.ReleaseMouseCapture();
                cameraContainer.Cursor = Cursors.Hand;
            }
            e.Handled = true;
        }
        
        private void CameraImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingCamera && currentZoom > 1.0)
            {
                Point currentPos = e.GetPosition(cameraContainer);
                double deltaX = currentPos.X - cameraDragStart.X;
                double deltaY = currentPos.Y - cameraDragStart.Y;
                
                cameraTranslate.X += deltaX;
                cameraTranslate.Y += deltaY;
                
                cameraDragStart = currentPos;
            }
        }
        
        #endregion

        private void StartCameraStream()
        {
            cameraActive = true;
            
            // Timer do odświeżania obrazu co 200ms (5 FPS)
            cameraTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            cameraTimer.Tick += async (s, e) => await RefreshCameraImage();
            cameraTimer.Start();
            
            // Pierwsze pobranie od razu
            _ = RefreshCameraImage();
        }

        private void StopCameraStream()
        {
            cameraActive = false;
            if (cameraTimer != null)
            {
                cameraTimer.Stop();
                cameraTimer = null;
            }
        }

        private async Task RefreshCameraImage()
        {
            if (!cameraActive) return;

            try
            {
                // Różne endpointy Hikvision - próbujemy po kolei
                string[] endpoints = new string[]
                {
                    $"http://{CAMERA_IP}/ISAPI/Streaming/channels/101/picture",
                    $"http://{CAMERA_IP}/ISAPI/Streaming/channels/1/picture",
                    $"http://{CAMERA_IP}/Streaming/channels/1/picture",
                    $"http://{CAMERA_IP}/cgi-bin/snapshot.cgi",
                    $"http://{CAMERA_IP}/snap.jpg",
                    $"http://{CAMERA_IP}/jpg/image.jpg",
                    $"http://{CAMERA_IP}/onvif-http/snapshot?Profile_1"
                };
                
                using (var handler = new HttpClientHandler())
                {
                    handler.Credentials = new System.Net.NetworkCredential(CAMERA_USER, CAMERA_PASS);
                    
                    using (var client = new HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(5);
                        
                        foreach (var url in endpoints)
                        {
                            try
                            {
                                var response = await client.GetAsync(url);
                                
                                if (response.IsSuccessStatusCode)
                                {
                                    var imageData = await response.Content.ReadAsByteArrayAsync();
                                    
                                    // Sprawdź czy to rzeczywiście obraz (JPEG zaczyna się od FF D8)
                                    if (imageData.Length > 2 && imageData[0] == 0xFF && imageData[1] == 0xD8)
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            if (!cameraActive) return;
                                            
                                            var bitmap = new BitmapImage();
                                            using (var ms = new MemoryStream(imageData))
                                            {
                                                bitmap.BeginInit();
                                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                                bitmap.StreamSource = ms;
                                                bitmap.EndInit();
                                                bitmap.Freeze();
                                            }
                                            cameraImage.Source = bitmap;
                                            cameraStatus.Text = "";
                                        });
                                        return; // Sukces - wychodzimy
                                    }
                                }
                            }
                            catch { /* Próbuj następny endpoint */ }
                        }
                        
                        // Żaden endpoint nie zadziałał
                        Dispatcher.Invoke(() =>
                        {
                            cameraStatus.Text = "Nie można połączyć z kamerą.\nSprawdź IP i hasło.";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    if (cameraActive)
                        cameraStatus.Text = $"Błąd: {ex.Message}";
                });
            }
        }

        #endregion

        #region ZAPIS ZDJĘĆ Z KAMERY

        private const string PHOTOS_BASE_PATH = @"\\192.168.0.170\Install\WagaSamochodowa";

        /// <summary>
        /// Pobiera snapshot z kamery i zapisuje do pliku
        /// </summary>
        /// <param name="tryb">AVILOG lub ODPADY</param>
        /// <param name="rejestracja">Numer rejestracyjny</param>
        /// <param name="rodzajWagi">TARA lub BRUTTO</param>
        /// <param name="towar">Rodzaj towaru (dla ODPADY)</param>
        /// <returns>Ścieżka do zapisanego pliku lub null jeśli błąd</returns>
        private async Task<string> SaveCameraSnapshot(string tryb, string rejestracja, string rodzajWagi, string towar = null)
        {
            Debug.WriteLine($"[SaveCameraSnapshot] START: tryb={tryb}, rej={rejestracja}, rodzaj={rodzajWagi}, towar={towar}");
            
            try
            {
                // Przygotuj folder z datą
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                string fullFolderPath = Path.Combine(PHOTOS_BASE_PATH, dateFolder);
                
                Debug.WriteLine($"[SaveCameraSnapshot] Folder: {fullFolderPath}");
                
                // Utwórz folder jeśli nie istnieje
                if (!Directory.Exists(fullFolderPath))
                {
                    Debug.WriteLine($"[SaveCameraSnapshot] Tworzę folder...");
                    Directory.CreateDirectory(fullFolderPath);
                }
                
                // Przygotuj nazwę pliku
                string timeStamp = DateTime.Now.ToString("HH-mm-ss");
                string safeRejestracja = rejestracja?.Replace(" ", "_").Replace("/", "-").Replace("\\", "-") ?? "BRAK";
                string fileName;
                
                if (tryb == "AVILOG")
                {
                    // AVILOG_06-30-15_WGM12345_BRUTTO.jpg
                    fileName = $"AVILOG_{timeStamp}_{safeRejestracja}_{rodzajWagi}.jpg";
                }
                else
                {
                    // ODPADY_08-15-22_ABC9876_KREW_TARA.jpg
                    fileName = $"ODPADY_{timeStamp}_{safeRejestracja}_{towar}_{rodzajWagi}.jpg";
                }
                
                string fullFilePath = Path.Combine(fullFolderPath, fileName);
                Debug.WriteLine($"[SaveCameraSnapshot] Plik: {fullFilePath}");
                
                // Pobierz zdjęcie z kamery
                Debug.WriteLine($"[SaveCameraSnapshot] Pobieram snapshot z kamery...");
                byte[] imageData = await GetCameraSnapshotBytes();
                
                if (imageData != null && imageData.Length > 0)
                {
                    Debug.WriteLine($"[SaveCameraSnapshot] Pobrano {imageData.Length} bajtów, zapisuję...");
                    // Zapisz do pliku
                    await Task.Run(() => File.WriteAllBytes(fullFilePath, imageData));
                    Debug.WriteLine($"[SaveCameraSnapshot] SUKCES: {fullFilePath}");
                    return fullFilePath;
                }
                
                Debug.WriteLine($"[SaveCameraSnapshot] BŁĄD: Brak danych z kamery");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FOTO] Błąd zapisu zdjęcia: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Pobiera bajty obrazu z kamery
        /// </summary>
        private async Task<byte[]> GetCameraSnapshotBytes()
        {
            string[] endpoints = new string[]
            {
                $"http://{CAMERA_IP}/ISAPI/Streaming/channels/101/picture",
                $"http://{CAMERA_IP}/ISAPI/Streaming/channels/1/picture",
                $"http://{CAMERA_IP}/Streaming/channels/1/picture",
                $"http://{CAMERA_IP}/cgi-bin/snapshot.cgi",
                $"http://{CAMERA_IP}/snap.jpg"
            };
            
            using (var handler = new HttpClientHandler())
            {
                handler.Credentials = new System.Net.NetworkCredential(CAMERA_USER, CAMERA_PASS);
                
                using (var client = new HttpClient(handler))
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    
                    foreach (var url in endpoints)
                    {
                        try
                        {
                            var response = await client.GetAsync(url);
                            
                            if (response.IsSuccessStatusCode)
                            {
                                var imageData = await response.Content.ReadAsByteArrayAsync();
                                
                                // Sprawdź czy to JPEG
                                if (imageData.Length > 2 && imageData[0] == 0xFF && imageData[1] == 0xD8)
                                {
                                    return imageData;
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// Wyświetla zdjęcie z ważenia w nowym oknie
        /// </summary>
        private void ShowWeightPhoto(string photoPath, string title)
        {
            if (string.IsNullOrEmpty(photoPath))
            {
                MessageBox.Show("Brak zdjęcia dla tego ważenia.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            if (!File.Exists(photoPath))
            {
                MessageBox.Show($"Plik nie istnieje:\n{photoPath}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                // Otwórz okno ze zdjęciem
                var photoWindow = new Window
                {
                    Title = title,
                    Width = 1000,
                    Height = 700,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 46))
                };
                
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(photoPath);
                bitmap.EndInit();
                
                var image = new Image
                {
                    Source = bitmap,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(10)
                };
                
                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                
                Grid.SetRow(image, 0);
                grid.Children.Add(image);
                
                var infoText = new TextBlock
                {
                    Text = photoPath,
                    Foreground = Brushes.Gray,
                    FontSize = 12,
                    Margin = new Thickness(10, 5, 10, 10),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(infoText, 1);
                grid.Children.Add(infoText);
                
                photoWindow.Content = grid;
                photoWindow.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania zdjęcia:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region ROZPOZNAWANIE TABLIC (Plate Recognizer API)

        // === KLUCZ API Plate Recognizer ===
        // Darmowe 2500 skanów/miesiąc - https://platerecognizer.com/
        private const string PLATE_RECOGNIZER_API_KEY = "e5c8ab0f0171e177faaf82a69427fb706a15bcbf";
        private const string PLATE_RECOGNIZER_URL = "https://api.platerecognizer.com/v1/plate-reader/";

        /// <summary>
        /// Rozpoznaje tablicę rejestracyjną ze zdjęcia z kamery (Plate Recognizer API)
        /// </summary>
        public async Task<AlprResult> RecognizePlateFromCamera()
        {
            var result = new AlprResult();
            
            try
            {
                // 1. Sprawdź czy klucz API jest ustawiony
                result.Steps.Add("[1] Sprawdzam klucz API...");
                
                if (string.IsNullOrEmpty(PLATE_RECOGNIZER_API_KEY) || PLATE_RECOGNIZER_API_KEY == "WKLEJ_TUTAJ_SWOJ_API_KEY")
                {
                    result.Error = "Brak klucza API!\n\n" +
                        "1. Zarejestruj się na https://platerecognizer.com/\n" +
                        "2. Skopiuj API Token z Dashboard\n" +
                        "3. Wklej do PanelPortiera.xaml.cs w linii:\n" +
                        "   PLATE_RECOGNIZER_API_KEY = \"twój_klucz\"";
                    result.Steps.Add("   ❌ BŁĄD: Klucz API nie jest skonfigurowany!");
                    return result;
                }
                result.Steps.Add("   ✓ Klucz API skonfigurowany");

                // 2. Pobierz zdjęcie z kamery
                result.Steps.Add($"[2] Pobieram zdjęcie z kamery {CAMERA_IP}...");
                byte[] imageData = await GetCameraSnapshotBytes();
                
                if (imageData == null || imageData.Length == 0)
                {
                    result.Error = "Nie udało się pobrać zdjęcia z kamery.\n\nSprawdź:\n• Czy kamera jest włączona\n• Czy IP jest prawidłowe\n• Czy hasło jest poprawne";
                    result.Steps.Add("   ❌ BŁĄD: Brak danych z kamery!");
                    return result;
                }
                result.Steps.Add($"   ✓ Pobrano {imageData.Length} bajtów ({imageData.Length / 1024} KB)");

                // 3. Zapisz zdjęcie do folderu diagnostycznego
                string tempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ALPR_Test");
                if (!Directory.Exists(tempFolder))
                    Directory.CreateDirectory(tempFolder);
                    
                string tempFile = Path.Combine(tempFolder, $"ALPR_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.jpg");
                result.Steps.Add($"[3] Zapisuję zdjęcie: {tempFile}");
                File.WriteAllBytes(tempFile, imageData);
                result.Steps.Add($"   ✓ Zdjęcie zapisane na pulpicie w: ALPR_Test");
                result.TempImagePath = tempFile;

                // 4. Wyślij do Plate Recognizer API
                result.Steps.Add("[4] Wysyłam do Plate Recognizer API...");
                
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Token {PLATE_RECOGNIZER_API_KEY}");
                    client.Timeout = TimeSpan.FromSeconds(30);
                    
                    using (var content = new MultipartFormDataContent())
                    {
                        var imageContent = new ByteArrayContent(imageData);
                        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                        content.Add(imageContent, "upload", "image.jpg");
                        
                        // Dodaj region Polski dla lepszej dokładności
                        content.Add(new StringContent("pl"), "regions");
                        
                        result.Steps.Add("   Wysyłam żądanie POST...");
                        
                        var response = await client.PostAsync(PLATE_RECOGNIZER_URL, content);
                        var responseBody = await response.Content.ReadAsStringAsync();
                        
                        result.RawOutput = responseBody;
                        result.Steps.Add($"   Status: {response.StatusCode}");
                        result.Steps.Add($"   Response length: {responseBody.Length} znaków");

                        if (!response.IsSuccessStatusCode)
                        {
                            result.Error = $"Błąd API: {response.StatusCode}\n\n{responseBody}";
                            result.Steps.Add($"   ❌ BŁĄD HTTP: {response.StatusCode}");
                            
                            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                            {
                                result.Error = "Nieprawidłowy klucz API lub wyczerpany limit.\n\n" +
                                    "Sprawdź na https://platerecognizer.com/\n" +
                                    "Dashboard → Usage";
                            }
                            return result;
                        }

                        // 5. Parsuj wynik JSON
                        result.Steps.Add("[5] Parsuję odpowiedź...");
                        result.Steps.Add($"   Pierwsze 500 znaków: {responseBody.Substring(0, Math.Min(500, responseBody.Length))}");
                        
                        var parseResult = ParsePlateRecognizerResult(responseBody);
                        result.Plate = parseResult.plate;
                        result.Confidence = parseResult.confidence;
                        result.PlatesFound = parseResult.platesFound;

                        if (parseResult.platesFound == 0)
                        {
                            result.Steps.Add("   ⚠ Nie znaleziono żadnych tablic na zdjęciu");
                            result.Error = "Nie wykryto tablicy na zdjęciu.\n\nMożliwe przyczyny:\n• Auto nie jest widoczne na kamerze\n• Tablica jest nieczytelna/brudna\n• Złe oświetlenie\n• Zły kąt kamery\n\nZdjęcie zapisane w: ALPR_Test na pulpicie";
                        }
                        else if (string.IsNullOrEmpty(parseResult.plate))
                        {
                            result.Steps.Add($"   ⚠ Znaleziono {parseResult.platesFound} tablic, ale pewność < 60%");
                            result.Error = $"Znaleziono tablicę, ale pewność jest zbyt niska ({parseResult.confidence:F1}%).\n\nPopraw widoczność tablicy.";
                        }
                        else
                        {
                            result.Steps.Add($"   ✓ Rozpoznano: {parseResult.plate} (pewność: {parseResult.confidence:F1}%)");
                            result.Success = true;
                        }
                    }
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                result.Error = $"Błąd połączenia z API:\n{ex.Message}\n\nSprawdź połączenie internetowe.";
                result.Steps.Add($"   ❌ BŁĄD HTTP: {ex.Message}");
                return result;
            }
            catch (TaskCanceledException)
            {
                result.Error = "Przekroczono czas oczekiwania (30s).\n\nSprawdź połączenie internetowe.";
                result.Steps.Add("   ❌ BŁĄD: Timeout");
                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Wyjątek: {ex.GetType().Name}\n\n{ex.Message}";
                result.Steps.Add($"   ❌ WYJĄTEK: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Parsuje wynik JSON z Plate Recognizer API
        /// </summary>
        private (string plate, double confidence, int platesFound) ParsePlateRecognizerResult(string json)
        {
            try
            {
                // Format: {"results":[{"box":{...},"plate":"ebr4h30","region":{...},"score":0.987,"candidates":[...],"dscore":0.776,...}]}
                // Uwaga: jest też "score":0.039 w region - to ignorujemy!
                
                string bestPlate = null;
                double bestConfidence = 0;
                int platesFound = 0;

                // Znajdź "results":[
                int resultsIndex = json.IndexOf("\"results\"");
                if (resultsIndex == -1)
                    return (null, 0, 0);

                // Znajdź "plate": w results
                int plateIndex = json.IndexOf("\"plate\"", resultsIndex);
                if (plateIndex == -1)
                    return (null, 0, 0);

                platesFound = 1;

                // Wyciągnij wartość plate
                int plateColon = json.IndexOf(":", plateIndex);
                int plateStartQuote = json.IndexOf("\"", plateColon + 1);
                int plateEndQuote = json.IndexOf("\"", plateStartQuote + 1);

                if (plateStartQuote != -1 && plateEndQuote != -1)
                {
                    bestPlate = json.Substring(plateStartQuote + 1, plateEndQuote - plateStartQuote - 1);
                }

                // Teraz szukaj "score": które jest NA TYM SAMYM POZIOMIE co plate
                // To znaczy szukamy wzorca: ,"score":X.XXX gdzie X > 0.5
                // Szukamy od pozycji plate do końca tego obiektu result
                
                // Znajdź wszystkie "score": i wybierz NAJWYŻSZĄ wartość
                int searchPos = plateEndQuote;
                int maxSearch = Math.Min(json.Length, searchPos + 500); // szukaj w następnych 500 znakach
                
                while (searchPos < maxSearch)
                {
                    int scoreIdx = json.IndexOf("\"score\"", searchPos);
                    if (scoreIdx == -1 || scoreIdx > maxSearch)
                        break;
                    
                    int scoreColon = json.IndexOf(":", scoreIdx);
                    if (scoreColon == -1)
                        break;
                        
                    int scoreEnd = json.IndexOfAny(new char[] { ',', '}', ']' }, scoreColon + 1);
                    if (scoreEnd == -1)
                        break;
                    
                    string scoreStr = json.Substring(scoreColon + 1, scoreEnd - scoreColon - 1).Trim();
                    
                    if (double.TryParse(scoreStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double score))
                    {
                        double scorePercent = score <= 1 ? score * 100 : score;
                        
                        // Bierzemy NAJWYŻSZY score
                        if (scorePercent > bestConfidence)
                        {
                            bestConfidence = scorePercent;
                        }
                    }
                    
                    searchPos = scoreEnd + 1;
                }

                // Formatuj tablicę
                if (!string.IsNullOrEmpty(bestPlate))
                {
                    bestPlate = FormatPolishPlate(bestPlate.ToUpper());
                }

                System.Diagnostics.Debug.WriteLine($"[ALPR] Parsed: plate={bestPlate}, confidence={bestConfidence:F1}%");

                // Zwróć jeśli mamy tablicę i pewność >= 50%
                if (!string.IsNullOrEmpty(bestPlate) && bestConfidence >= 50)
                    return (bestPlate, bestConfidence, platesFound);
                else if (!string.IsNullOrEmpty(bestPlate))
                    return (null, bestConfidence, platesFound); // znaleziono ale niska pewność
                else
                    return (null, 0, 0);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ALPR] Parse error: {ex.Message}");
                return (null, 0, 0);
            }
        }

        /// <summary>
        /// Formatuje polską tablicę rejestracyjną (dodaje spację)
        /// </summary>
        private string FormatPolishPlate(string plate)
        {
            if (string.IsNullOrEmpty(plate) || plate.Length < 4)
                return plate;

            // Polskie tablice: 2-3 litery + cyfry/litery
            // Np. EBR4H30 -> EBR 4H30, WGM12345 -> WGM 12345
            
            // Znajdź gdzie kończą się litery na początku
            int letterCount = 0;
            foreach (char c in plate)
            {
                if (char.IsLetter(c))
                    letterCount++;
                else
                    break;
            }

            if (letterCount >= 2 && letterCount <= 3 && letterCount < plate.Length)
            {
                return plate.Substring(0, letterCount) + " " + plate.Substring(letterCount);
            }

            return plate;
        }

        /// <summary>
        /// Klasa wyniku ALPR z diagnostyką
        /// </summary>
        public class AlprResult
        {
            public bool Success { get; set; }
            public string Plate { get; set; }
            public double Confidence { get; set; }
            public int PlatesFound { get; set; }
            public string Error { get; set; }
            public string RawOutput { get; set; }
            public string RawError { get; set; }
            public int ExitCode { get; set; }
            public string TempImagePath { get; set; }
            public List<string> Steps { get; set; } = new List<string>();

            public string GetDiagnostics()
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("═══════════════════════════════════════");
                sb.AppendLine("   DIAGNOSTYKA - Plate Recognizer");
                sb.AppendLine("═══════════════════════════════════════\n");
                
                foreach (var step in Steps)
                    sb.AppendLine(step);
                
                sb.AppendLine("\n═══════════════════════════════════════");
                sb.AppendLine($"WYNIK: {(Success ? "✓ SUKCES" : "❌ BŁĄD")}");
                
                if (Success)
                {
                    sb.AppendLine($"TABLICA: {Plate}");
                    sb.AppendLine($"PEWNOŚĆ: {Confidence:F1}%");
                }
                else if (!string.IsNullOrEmpty(Error))
                {
                    sb.AppendLine($"\nBŁĄD:\n{Error}");
                }
                
                sb.AppendLine("═══════════════════════════════════════");
                
                return sb.ToString();
            }
        }

        /// <summary>
        /// Przycisk skanowania tablicy rejestracyjnej
        /// </summary>
        public async void BtnScanPlate_Click(object sender, RoutedEventArgs e)
        {
            var originalContent = (sender as Button)?.Content;
            if (sender is Button btn)
            {
                btn.IsEnabled = false;
            }

            try
            {
                var result = await RecognizePlateFromCamera();
                HandleAlprResult(result);
            }
            catch (Exception ex)
            {
                PlaySound(false);
                MessageBox.Show($"Nieoczekiwany błąd:\n\n{ex.Message}\n\n{ex.StackTrace}", 
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is Button btn2)
                {
                    btn2.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Wgraj zdjęcie z dysku i rozpoznaj tablicę
        /// </summary>
        public async void BtnLoadImageForALPR_Click(object sender, RoutedEventArgs e)
        {
            // Otwórz dialog wyboru pliku
            var dialog = new OpenFileDialog
            {
                Title = "Wybierz zdjęcie do rozpoznania tablicy",
                Filter = "Obrazy|*.jpg;*.jpeg;*.png;*.bmp|Wszystkie pliki|*.*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            };

            // Sprawdź czy jest folder ALPR_Test na pulpicie
            string alprFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ALPR_Test");
            if (Directory.Exists(alprFolder))
            {
                dialog.InitialDirectory = alprFolder;
            }
            
            // Sprawdź też folder zdjęć z wagi
            string wagaFolder = @"\\192.168.0.170\Install\WagaSamochodowa";
            if (Directory.Exists(wagaFolder))
            {
                // Znajdź najnowszy folder z datą
                try
                {
                    var dirs = Directory.GetDirectories(wagaFolder).OrderByDescending(d => d).FirstOrDefault();
                    if (!string.IsNullOrEmpty(dirs))
                        dialog.InitialDirectory = dirs;
                }
                catch { }
            }

            if (dialog.ShowDialog() != true)
                return;

            if (sender is Button btn)
                btn.IsEnabled = false;

            try
            {
                var result = await RecognizePlateFromFile(dialog.FileName);
                HandleAlprResult(result);
            }
            catch (Exception ex)
            {
                PlaySound(false);
                MessageBox.Show($"Nieoczekiwany błąd:\n\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is Button btn2)
                    btn2.IsEnabled = true;
            }
        }

        /// <summary>
        /// Rozpoznaje tablicę z pliku na dysku
        /// </summary>
        private async Task<AlprResult> RecognizePlateFromFile(string filePath)
        {
            var result = new AlprResult();

            try
            {
                result.Steps.Add($"[1] Wczytuję plik: {filePath}");

                if (!File.Exists(filePath))
                {
                    result.Error = $"Plik nie istnieje:\n{filePath}";
                    result.Steps.Add("   ❌ BŁĄD: Plik nie istnieje!");
                    return result;
                }

                byte[] imageData = File.ReadAllBytes(filePath);
                result.Steps.Add($"   ✓ Wczytano {imageData.Length} bajtów ({imageData.Length / 1024} KB)");
                result.TempImagePath = filePath;

                // Wyślij do API
                result.Steps.Add("[2] Wysyłam do Plate Recognizer API...");

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Token {PLATE_RECOGNIZER_API_KEY}");
                    client.Timeout = TimeSpan.FromSeconds(30);

                    using (var content = new MultipartFormDataContent())
                    {
                        var imageContent = new ByteArrayContent(imageData);
                        string mimeType = "image/jpeg";
                        if (filePath.ToLower().EndsWith(".png"))
                            mimeType = "image/png";
                        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mimeType);
                        content.Add(imageContent, "upload", Path.GetFileName(filePath));
                        content.Add(new StringContent("pl"), "regions");

                        var response = await client.PostAsync(PLATE_RECOGNIZER_URL, content);
                        var responseBody = await response.Content.ReadAsStringAsync();

                        result.RawOutput = responseBody;
                        result.Steps.Add($"   Status: {response.StatusCode}");

                        if (!response.IsSuccessStatusCode)
                        {
                            result.Error = $"Błąd API: {response.StatusCode}\n\n{responseBody}";
                            result.Steps.Add($"   ❌ BŁĄD HTTP: {response.StatusCode}");
                            return result;
                        }

                        result.Steps.Add("[3] Parsuję odpowiedź...");
                        result.Steps.Add($"   Pierwsze 400 znaków: {responseBody.Substring(0, Math.Min(400, responseBody.Length))}");

                        var parseResult = ParsePlateRecognizerResult(responseBody);
                        result.Plate = parseResult.plate;
                        result.Confidence = parseResult.confidence;
                        result.PlatesFound = parseResult.platesFound;

                        if (parseResult.platesFound == 0)
                        {
                            result.Steps.Add("   ⚠ Nie znaleziono żadnych tablic na zdjęciu");
                            result.Error = "Nie wykryto tablicy na zdjęciu.\n\nSprawdź czy tablica jest widoczna.";
                        }
                        else if (string.IsNullOrEmpty(parseResult.plate))
                        {
                            result.Steps.Add($"   ⚠ Pewność zbyt niska: {parseResult.confidence:F1}%");
                            result.Error = $"Znaleziono tablicę, ale pewność jest zbyt niska ({parseResult.confidence:F1}%).";
                        }
                        else
                        {
                            result.Steps.Add($"   ✓ Rozpoznano: {parseResult.plate} ({parseResult.confidence:F1}%)");
                            result.Success = true;
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                result.Error = $"Wyjątek: {ex.Message}";
                result.Steps.Add($"   ❌ WYJĄTEK: {ex.Message}");
                return result;
            }
        }

        /// <summary>
        /// Obsługuje wynik ALPR - pokazuje komunikat lub wstawia tablicę
        /// </summary>
        private void HandleAlprResult(AlprResult result)
        {
            if (result.Success && !string.IsNullOrEmpty(result.Plate))
            {
                // Znaleziono tablicę
                if (aktualnyTryb == "Odpady" && txtEditRejestracja != null)
                {
                    txtEditRejestracja.Text = result.Plate;
                    PlaySound(true);
                    MessageBox.Show($"✓ Rozpoznano tablicę:\n\n{result.Plate}\n\nPewność: {result.Confidence:F1}%",
                        "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    PlaySound(true);
                    MessageBox.Show($"✓ Rozpoznano tablicę:\n\n{result.Plate}\n\nPewność: {result.Confidence:F1}%\n\n(W trybie AVILOG tablica jest przypisana do dostawy)",
                        "Rozpoznana tablica", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                PlaySound(false);
                string diagnostics = result.GetDiagnostics();
                MessageBox.Show(diagnostics, "Diagnostyka ALPR", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Flaga - czy okno kamery jest otwarte w trybie skanowania do TextBox
        private bool cameraScanToTextBoxMode = false;

        /// <summary>
        /// Otwórz okno kamery w trybie skanowania do TextBox (mały przycisk przy polu rejestracji)
        /// </summary>
        public void BtnScanPlateToTextBox_Click(object sender, RoutedEventArgs e)
        {
            // Ustaw tryb skanowania do TextBox
            cameraScanToTextBoxMode = true;
            
            // Otwórz okno kamery
            CameraOverlay.Visibility = Visibility.Visible;
            cameraStatus.Text = "Łączenie z kamerą...";
            cameraImage.Source = null;
            
            // Reset zoom przy otwarciu
            ResetCameraZoom();
            
            StartCameraStream();
        }

        /// <summary>
        /// Skanuj tablicę - wersja dla okna kamery (obsługuje tryb TextBox)
        /// </summary>
        public async void BtnScanPlateInCameraWindow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
                btn.IsEnabled = false;

            try
            {
                var result = await RecognizePlateFromCamera();

                if (result.Success && !string.IsNullOrEmpty(result.Plate))
                {
                    // Znaleziono tablicę
                    PlaySound(true);
                    
                    if (cameraScanToTextBoxMode && txtEditRejestracja != null)
                    {
                        // Tryb skanowania do TextBox - wpisz do pola
                        txtEditRejestracja.Text = result.Plate;
                        
                        MessageBox.Show($"✓ Rozpoznano tablicę:\n\n{result.Plate}\n\nPewność: {result.Confidence:F1}%",
                            "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        // Normalny tryb - tylko pokaż wynik
                        MessageBox.Show($"✓ Rozpoznano tablicę:\n\n{result.Plate}\n\nPewność: {result.Confidence:F1}%",
                            "Rozpoznana tablica", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    PlaySound(false);
                    string diagnostics = result.GetDiagnostics();
                    MessageBox.Show(diagnostics, "Diagnostyka ALPR", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                PlaySound(false);
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (sender is Button btn2)
                    btn2.IsEnabled = true;
            }
        }

        #endregion

        #region DRUKOWANIE PARAGONÓW

        // Nazwa drukarki - PICCO lub pierwsza dostępna drukarka POS
        private string printerName = null;

        /// <summary>
        /// Znajduje drukarkę PICCO lub podobną drukarkę termiczną
        /// </summary>
        private string FindReceiptPrinter()
        {
            try
            {
                foreach (string printer in PrinterSettings.InstalledPrinters)
                {
                    string upper = printer.ToUpper();
                    if (upper.Contains("PICCO") || upper.Contains("POS") || upper.Contains("THERMAL") || 
                        upper.Contains("58") || upper.Contains("80") || upper.Contains("RECEIPT"))
                    {
                        Debug.WriteLine($"[DRUKARKA] Znaleziono: {printer}");
                        return printer;
                    }
                }
                
                // Jeśli nie znaleziono specyficznej, użyj domyślnej
                var settings = new PrinterSettings();
                if (!string.IsNullOrEmpty(settings.PrinterName))
                {
                    Debug.WriteLine($"[DRUKARKA] Używam domyślnej: {settings.PrinterName}");
                    return settings.PrinterName;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DRUKARKA] Błąd szukania: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Aktualizuje status drukarki na pasku
        /// </summary>
        private void UpdatePrinterStatus(string status, bool isError = false)
        {
            Dispatcher.Invoke(() =>
            {
                if (lblPrinterStatus != null)
                {
                    lblPrinterStatus.Text = status;
                    lblPrinterStatus.Foreground = isError 
                        ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF5555"))
                        : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50"));
                }
                if (lblPrinterIcon != null)
                {
                    lblPrinterIcon.Foreground = isError
                        ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF5555"))
                        : new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50"));
                }
            });

            // Wyczyść status po 5 sekundach
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                Dispatcher.Invoke(() =>
                {
                    if (lblPrinterStatus != null)
                    {
                        lblPrinterStatus.Text = "";
                        lblPrinterIcon.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#888888"));
                    }
                });
            };
            timer.Start();
        }

        /// <summary>
        /// Drukuje paragon po zapisie ważenia
        /// </summary>
        private void PrintReceipt(string rejestracja, string towar, string odbiorca, int brutto, int tara, int netto, string typ)
        {
            Task.Run(() =>
            {
                try
                {
                    if (printerName == null)
                        printerName = FindReceiptPrinter();

                    if (string.IsNullOrEmpty(printerName))
                    {
                        UpdatePrinterStatus("Brak drukarki", true);
                        return;
                    }

                    var printDoc = new PrintDocument();
                    printDoc.PrinterSettings.PrinterName = printerName;

                    if (!printDoc.PrinterSettings.IsValid)
                    {
                        UpdatePrinterStatus($"Drukarka niedostępna", true);
                        return;
                    }

                    // Dane do wydruku
                    string printRejestracja = rejestracja;
                    string printTowar = towar;
                    string printOdbiorca = odbiorca;
                    int printBrutto = brutto;
                    int printTara = tara;
                    int printNetto = netto;
                    string printTyp = typ;
                    DateTime printDate = DateTime.Now;

                    printDoc.PrintPage += (sender, e) =>
                    {
                        // Czcionki
                        System.Drawing.Font fontTitle = new System.Drawing.Font("Arial", 10, System.Drawing.FontStyle.Bold);
                        System.Drawing.Font fontNormal = new System.Drawing.Font("Arial", 8);
                        System.Drawing.Font fontBold = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold);
                        System.Drawing.Font fontBig = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);

                        float x = 5;
                        float y = 5;
                        float lineHeight = 14;
                        float width = e.PageBounds.Width - 10;

                        // Nagłówek
                        string header = "UBOJNIA DROBIU";
                        var headerSize = e.Graphics.MeasureString(header, fontTitle);
                        e.Graphics.DrawString(header, fontTitle, System.Drawing.Brushes.Black, (width - headerSize.Width) / 2 + x, y);
                        y += lineHeight + 2;

                        string header2 = "PIÓRKOWSCY";
                        var header2Size = e.Graphics.MeasureString(header2, fontTitle);
                        e.Graphics.DrawString(header2, fontTitle, System.Drawing.Brushes.Black, (width - header2Size.Width) / 2 + x, y);
                        y += lineHeight + 5;

                        // Linia
                        e.Graphics.DrawLine(System.Drawing.Pens.Black, x, y, x + width, y);
                        y += 5;

                        // Data i godzina
                        e.Graphics.DrawString($"Data: {printDate:dd.MM.yyyy}  Godz: {printDate:HH:mm}", fontNormal, System.Drawing.Brushes.Black, x, y);
                        y += lineHeight + 3;

                        // Typ ważenia
                        e.Graphics.DrawString($"Ważenie: {printTyp}", fontBold, System.Drawing.Brushes.Black, x, y);
                        y += lineHeight + 5;

                        // Linia
                        e.Graphics.DrawLine(System.Drawing.Pens.Black, x, y, x + width, y);
                        y += 5;

                        // Dane
                        e.Graphics.DrawString($"Rejestracja:", fontNormal, System.Drawing.Brushes.Black, x, y);
                        y += lineHeight;
                        e.Graphics.DrawString($"  {printRejestracja}", fontBold, System.Drawing.Brushes.Black, x, y);
                        y += lineHeight + 3;

                        if (!string.IsNullOrEmpty(printTowar))
                        {
                            e.Graphics.DrawString($"Towar: {printTowar}", fontNormal, System.Drawing.Brushes.Black, x, y);
                            y += lineHeight;
                        }

                        if (!string.IsNullOrEmpty(printOdbiorca))
                        {
                            e.Graphics.DrawString($"Odbiorca: {printOdbiorca}", fontNormal, System.Drawing.Brushes.Black, x, y);
                            y += lineHeight + 5;
                        }

                        // Linia
                        e.Graphics.DrawLine(System.Drawing.Pens.Black, x, y, x + width, y);
                        y += 5;

                        // Wagi
                        if (printTara > 0)
                        {
                            e.Graphics.DrawString($"TARA:    {printTara:N0} kg", fontNormal, System.Drawing.Brushes.Black, x, y);
                            y += lineHeight;
                        }

                        if (printBrutto > 0)
                        {
                            e.Graphics.DrawString($"BRUTTO:  {printBrutto:N0} kg", fontNormal, System.Drawing.Brushes.Black, x, y);
                            y += lineHeight;
                        }

                        if (printNetto > 0)
                        {
                            y += 3;
                            e.Graphics.DrawLine(System.Drawing.Pens.Black, x, y, x + width, y);
                            y += 5;
                            e.Graphics.DrawString($"NETTO:   {printNetto:N0} kg", fontBig, System.Drawing.Brushes.Black, x, y);
                            y += lineHeight + 5;
                        }

                        // Linia końcowa
                        e.Graphics.DrawLine(System.Drawing.Pens.Black, x, y, x + width, y);
                        y += 10;

                        // Podpis
                        e.Graphics.DrawString("Podpis kierowcy:", fontNormal, System.Drawing.Brushes.Black, x, y);
                        y += lineHeight + 15;
                        e.Graphics.DrawLine(System.Drawing.Pens.Black, x, y, x + width - 20, y);

                        // Stopka
                        y += 20;
                        e.Graphics.DrawLine(System.Drawing.Pens.Black, x, y, x + width, y);
                    };

                    printDoc.Print();
                    UpdatePrinterStatus($"✓ Wydrukowano {printTyp}", false);
                    Debug.WriteLine($"[DRUKARKA] Wydrukowano paragon: {printTyp} - {printRejestracja}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DRUKARKA] Błąd drukowania: {ex.Message}");
                    UpdatePrinterStatus($"Błąd: {ex.Message}", true);
                }
            });
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

        private string _nrRejestracyjny;
        public string NrRejestracyjny
        {
            get => _nrRejestracyjny;
            set
            {
                _nrRejestracyjny = value;
                OnPropertyChanged(nameof(NrRejestracyjny));
            }
        }

        public string Towar { get; set; }
        public int SztukiPlan { get; set; }
        public string GodzinaTaraDisplay { get; set; } = "-";
        public string GodzinaBruttoDisplay { get; set; } = "-";
        
        // Ścieżki do zdjęć z ważenia
        public string ZdjecieTaraPath { get; set; }
        public string ZdjecieBruttoPath { get; set; }

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