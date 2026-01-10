using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Kalendarz1
{
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class Odbiorca
    {
        public string ID { get; set; }
        public string Nazwa { get; set; }
    }

    public partial class PanelPortiera : Window, INotifyPropertyChanged
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private ObservableCollection<DostawaPortiera> dostawy;
        private DostawaPortiera _wybranaDostwa;

        public DostawaPortiera WybranaDostwa
        {
            get => _wybranaDostwa;
            set { _wybranaDostwa = value; OnPropertyChanged(nameof(WybranaDostwa)); }
        }

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set { if (_selectedDate != value) { _selectedDate = value; OnPropertyChanged(nameof(SelectedDate)); LoadDostawy(); } }
        }

        private enum AktywnePole { Brutto, Tara }
        private AktywnePole aktywnePole = AktywnePole.Brutto;
        private string aktualnyTryb = "Avilog";

        private SerialPort serialPort;
        private DispatcherTimer autoRefreshTimer;
        private DispatcherTimer clockTimer;

        public ObservableCollection<Odbiorca> ListaOdbiorcow { get; set; } = new ObservableCollection<Odbiorca>();

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
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDostawy();
            // LoadOdbiorcy() wywołujemy teraz dynamicznie przy zmianie towaru

            autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
            autoRefreshTimer.Tick += (s, ev) => LoadDostawy();
            autoRefreshTimer.Start();
            clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clockTimer.Tick += (s, ev) => lblTime.Text = DateTime.Now.ToString("HH:mm");
            clockTimer.Start();
            lblTime.Text = DateTime.Now.ToString("HH:mm");
            ConnectToScale("COM3", 9600);
        }

        // === LOGIKA ODBIORCÓW NA SZTYWNO ===
        private void UpdateOdbiorcy(string towar)
        {
            ListaOdbiorcow.Clear();
            string domyslny = "";

            if (towar == "JELITA")
            {
                ListaOdbiorcow.Add(new Odbiorca { ID = "1", Nazwa = "Jasta" });
                ListaOdbiorcow.Add(new Odbiorca { ID = "2", Nazwa = "General Food Supply" });
                domyslny = "Jasta";
            }
            else if (towar == "PIÓRA")
            {
                ListaOdbiorcow.Add(new Odbiorca { ID = "1", Nazwa = "Jasta" });
                domyslny = "Jasta";
            }
            else if (towar == "KREW")
            {
                ListaOdbiorcow.Add(new Odbiorca { ID = "1", Nazwa = "Jasta" });
                ListaOdbiorcow.Add(new Odbiorca { ID = "2", Nazwa = "General Food Supply" });
                // Brak domyślnego w wymaganiach, ale ustawmy pierwszy
                domyslny = "Jasta";
            }
            else if (towar == "ŁAPY")
            {
                ListaOdbiorcow.Add(new Odbiorca { ID = "3", Nazwa = "Marcin Piórkowski" });
                domyslny = "Marcin Piórkowski";
            }

            // Ustaw domyślnego
            if (!string.IsNullOrEmpty(domyslny))
                cbOdbiorcy.Text = domyslny;
        }

        private void UpdateThemeColor(string towar)
        {
            Color color;
            if (towar == "KREW") color = (Color)ColorConverter.ConvertFromString("#D32F2F"); // Czerwony
            else if (towar == "ŁAPY") color = (Color)ColorConverter.ConvertFromString("#FFC107"); // Żółty/Bursztynowy
            else if (towar == "PIÓRA") color = (Color)ColorConverter.ConvertFromString("#9E9E9E"); // Szary/Biały (żeby tekst był widoczny)
            else if (towar == "JELITA") color = (Color)ColorConverter.ConvertFromString("#795548"); // Brązowy (Odpady)
            else color = (Color)ColorConverter.ConvertFromString("#F44336"); // Domyślny

            this.Resources["ThemeColor"] = new SolidColorBrush(color);
        }

        public void BtnCommodity_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb)
            {
                string towar = rb.Content.ToString();

                // Aktualizuj kolory interfejsu
                UpdateThemeColor(towar);

                // Aktualizuj listę odbiorców
                UpdateOdbiorcy(towar);

                if (WybranaDostwa != null)
                {
                    WybranaDostwa.Towar = towar;
                    // Jeśli to nowy wpis, ustaw też domyślnego odbiorcę od razu w obiekcie
                    if (cbOdbiorcy.Items.Count > 0)
                        WybranaDostwa.HodowcaNazwa = cbOdbiorcy.Text;
                }
            }
        }

        // === KLAWIATURA EKRANOWA ===
        private void TxtEditRejestracja_Click(object sender, MouseButtonEventArgs e)
        {
            KeyboardOverlay.Visibility = Visibility.Visible;
        }

        private void CloseKeyboard_Click(object sender, RoutedEventArgs e)
        {
            KeyboardOverlay.Visibility = Visibility.Collapsed;
        }

        private void Keyboard_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                txtEditRejestracja.Text += btn.Content.ToString();
                // Aktualizuj też obiekt
                if (WybranaDostwa != null) WybranaDostwa.NrRejestracyjny = txtEditRejestracja.Text;
            }
        }

        private void KeyboardBackspace_Click(object sender, RoutedEventArgs e)
        {
            if (txtEditRejestracja.Text.Length > 0)
            {
                txtEditRejestracja.Text = txtEditRejestracja.Text.Substring(0, txtEditRejestracja.Text.Length - 1);
                if (WybranaDostwa != null) WybranaDostwa.NrRejestracyjny = txtEditRejestracja.Text;
            }
        }


        // === RESZTA KODU BEZ ZMIAN ===
        public void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Zamknąć system?", "Wyjście", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                if (serialPort != null && serialPort.IsOpen) serialPort.Close();
                this.Close();
            }
        }

        public void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadDostawy();
        public void PrevDay_Click(object sender, RoutedEventArgs e) => SelectedDate = SelectedDate.AddDays(-1);
        public void NextDay_Click(object sender, RoutedEventArgs e) => SelectedDate = SelectedDate.AddDays(1);
        public void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is DatePicker dp && dp.SelectedDate.HasValue) SelectedDate = dp.SelectedDate.Value;
        }

        public void ZmienTryb_Click(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag != null)
            {
                string tag = rb.Tag.ToString();
                if (tag == "🐔") aktualnyTryb = "Avilog";
                else aktualnyTryb = "Odpady";

                if (aktualnyTryb == "Avilog")
                {
                    this.Resources["ThemeColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA726"));
                    lblHeaderTitle.Text = "LISTA: AVILOG";
                    viewTiles.Visibility = Visibility.Visible;
                    gridTable.Visibility = Visibility.Collapsed;
                    panelButtonsLeft.Visibility = Visibility.Collapsed;
                    panelCommodity.Visibility = Visibility.Collapsed;
                    lblOdbiorcaTitle.Text = "Kierowca:";
                }
                else
                {
                    // Domyślnie czerwony dla odpadów, ale zmieni się po kliknięciu towaru
                    this.Resources["ThemeColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
                    lblHeaderTitle.Text = "LISTA: ODPADY";
                    viewTiles.Visibility = Visibility.Collapsed;
                    gridTable.Visibility = Visibility.Visible;
                    panelButtonsLeft.Visibility = Visibility.Visible;
                    panelCommodity.Visibility = Visibility.Visible;
                    lblOdbiorcaTitle.Text = "Odbiorca:";

                    // Domyślnie zaznacz Krew
                    btnKrew.IsChecked = true;
                    BtnCommodity_Click(btnKrew, null);
                }

                LoadDostawy();
                ClearWybranaDostwa();
            }
        }

        private void LoadDostawy()
        {
            try
            {
                dostawy.Clear();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    if (aktualnyTryb == "Avilog")
                    {
                        string query = @"SELECT fc.ID, fc.LpDostawy, fc.CustomerGID,
                            (SELECT TOP 1 ShortName FROM dbo.Dostawcy WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(fc.CustomerGID))) as HodowcaNazwa,
                            ISNULL(dr.[Name], '') as KierowcaNazwa, fc.CarID, fc.TrailerID, fc.SztPoj,
                            ISNULL(fc.FullFarmWeight, 0) as Brutto, ISNULL(fc.EmptyFarmWeight, 0) as Tara, ISNULL(fc.NettoFarmWeight, 0) as Netto
                        FROM dbo.FarmerCalc fc
                        LEFT JOIN dbo.Driver dr ON fc.DriverGID = dr.GID
                        WHERE CAST(fc.CalcDate AS DATE) = @Data
                        ORDER BY CASE WHEN ISNUMERIC(fc.LpDostawy) = 1 THEN CAST(fc.LpDostawy AS INT) ELSE 999999 END, fc.ID";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Data", SelectedDate);
                            using (SqlDataReader r = cmd.ExecuteReader())
                            {
                                int lp = 1;
                                while (r.Read())
                                {
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
                                        Towar = "Żywiec"
                                    };
                                    d.NrRejestracyjny = $"{d.CarID} {d.TrailerID}";
                                    d.GodzinaPrzyjazdu = new DateTime(SelectedDate.Year, SelectedDate.Month, SelectedDate.Day, 6, 0, 0).AddMinutes((lp - 1) * 15).ToString("HH:mm");
                                    d.UpdateStatus();
                                    dostawy.Add(d);
                                }
                            }
                        }
                    }
                    else
                    {
                        string query = @"
                            SELECT ID, Format(DataWazenia, 'HH:mm') as Godzina, NrRejestracyjny, Odbiorca, Brutto, Tara, Netto, Towar
                            FROM dbo.OdpadyRejestr
                            WHERE CAST(DataWazenia AS DATE) = @Data
                            ORDER BY DataWazenia DESC";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@Data", SelectedDate);
                            using (SqlDataReader r = cmd.ExecuteReader())
                            {
                                while (r.Read())
                                {
                                    var d = new DostawaPortiera
                                    {
                                        ID = Convert.ToInt64(r["ID"]),
                                        GodzinaPrzyjazdu = r["Godzina"].ToString(),
                                        NrRejestracyjny = r["NrRejestracyjny"].ToString(),
                                        HodowcaNazwa = r["Odbiorca"].ToString(),
                                        Brutto = Convert.ToInt32(r["Brutto"]),
                                        Tara = Convert.ToInt32(r["Tara"]),
                                        Netto = Convert.ToInt32(r["Netto"]),
                                        Towar = r["Towar"].ToString(),
                                        Lp = "-",
                                        CarID = "",
                                        TrailerID = "",
                                        KierowcaNazwa = "",
                                        SztukiPlan = 0
                                    };
                                    d.UpdateStatus();
                                    dostawy.Add(d);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public void Dostawa_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is DostawaPortiera dostawa)
                WybierzDostawe(dostawa);
        }

        private void GridTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (gridTable.SelectedItem is DostawaPortiera dostawa)
                WybierzDostawe(dostawa);
        }

        private void WybierzDostawe(DostawaPortiera dostawa)
        {
            WybranaDostwa = dostawa;

            if (aktualnyTryb == "Avilog")
            {
                lblTrybPracy.Text = "PODGLĄD";
                panelReadOnlyCar.Visibility = Visibility.Visible;
                panelEditCar.Visibility = Visibility.Collapsed;
                panelReadOnlyOdbiorca.Visibility = Visibility.Visible;
                panelEditOdbiorca.Visibility = Visibility.Collapsed;
                panelCommodity.Visibility = Visibility.Collapsed;

                lblWybranyPojazd.Text = dostawa.CarID;
                lblWybranaNaczepa.Text = dostawa.TrailerID;
                lblWybranyKierowca.Text = dostawa.KierowcaNazwa;
                lblWybranyHodowca.Text = dostawa.HodowcaNazwa;
            }
            else
            {
                lblTrybPracy.Text = "EDYCJA DANYCH";
                panelReadOnlyCar.Visibility = Visibility.Collapsed;
                panelEditCar.Visibility = Visibility.Visible;
                txtEditRejestracja.Text = dostawa.NrRejestracyjny;

                panelReadOnlyOdbiorca.Visibility = Visibility.Collapsed;
                panelEditOdbiorca.Visibility = Visibility.Visible;

                // Ustaw Odbiorcę i Towar (Przyciski nad wagą)
                if (!string.IsNullOrEmpty(dostawa.HodowcaNazwa)) cbOdbiorcy.Text = dostawa.HodowcaNazwa;
                else cbOdbiorcy.SelectedIndex = -1;

                if (dostawa.Towar == "KREW") btnKrew.IsChecked = true;
                else if (dostawa.Towar == "ŁAPY") btnLapy.IsChecked = true;
                else if (dostawa.Towar == "PIÓRA") btnPiora.IsChecked = true;
                else if (dostawa.Towar == "JELITA") btnJelita.IsChecked = true;

                // Wywołaj aktualizację kolorów i list dla wybranego towaru
                BtnCommodity_Click((dostawa.Towar == "KREW" ? btnKrew : (dostawa.Towar == "ŁAPY" ? btnLapy : (dostawa.Towar == "PIÓRA" ? btnPiora : btnJelita))), null);

                panelCommodity.Visibility = Visibility.Visible;
            }

            lblPlanowaneSztuki.Text = dostawa.SztukiPlanDisplay;
            lblWagiBaza.Text = $"Baza: B={dostawa.Brutto} | T={dostawa.Tara}";

            txtBrutto.Text = dostawa.Brutto.ToString();
            txtTara.Text = dostawa.Tara.ToString();
            UpdateBigDisplay();
            btnZapisz.IsEnabled = true;
            btnDrukuj.IsEnabled = dostawa.Netto > 0;
            btnDelete.IsEnabled = true;
        }

        private void ClearWybranaDostwa()
        {
            WybranaDostwa = null;
            lblWybranyPojazd.Text = "---"; lblWybranaNaczepa.Text = "---"; lblWagiBaza.Text = "";
            lblWybranyOdbiorca.Text = "---"; lblWybranyHodowca.Text = "";
            txtBrutto.Text = "0"; txtTara.Text = "0"; BigWeightDisplay.Text = "0";
            btnZapisz.IsEnabled = false; btnDelete.IsEnabled = false;
        }

        public void Mode_Click(object sender, RoutedEventArgs e)
        {
            aktywnePole = (radioBrutto.IsChecked == true) ? AktywnePole.Brutto : AktywnePole.Tara;
            UpdateBigDisplay();
        }

        private void UpdateBigDisplay()
        {
            if (WybranaDostwa == null) { BigWeightDisplay.Text = "0"; return; }
            string rawValue = (aktywnePole == AktywnePole.Brutto) ? txtBrutto.Text : txtTara.Text;

            if (int.TryParse(rawValue, out int val))
            {
                var nfi = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
                nfi.NumberGroupSeparator = " ";
                BigWeightDisplay.Text = val.ToString("N0", nfi);
            }
            else BigWeightDisplay.Text = "0";
        }

        public void NumpadClick(object sender, RoutedEventArgs e)
        {
            if (WybranaDostwa == null) return;
            if (sender is Button btn)
            {
                TextBlock target = (aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara;
                if (target.Text == "0") target.Text = "";
                if (target.Text.Length < 6) target.Text += btn.Content.ToString();
                UpdateBigDisplay();
            }
        }

        public void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (WybranaDostwa == null) return;
            ((aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara).Text = "0";
            UpdateBigDisplay();
        }

        public void BtnBackspace_Click(object sender, RoutedEventArgs e)
        {
            if (WybranaDostwa == null) return;
            TextBlock target = (aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara;
            if (target.Text.Length > 0) target.Text = target.Text.Substring(0, target.Text.Length - 1);
            if (string.IsNullOrEmpty(target.Text)) target.Text = "0";
            UpdateBigDisplay();
        }

        public void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (WybranaDostwa == null) return;
            try
            {
                int.TryParse(txtBrutto.Text, out int b);
                int.TryParse(txtTara.Text, out int t);

                if (t > b && b > 0)
                {
                    MessageBox.Show("BŁĄD: Tara > Brutto!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (b > 0 && b < 5000 && t == 0)
                {
                    if (MessageBox.Show($"Waga {b} kg podejrzanie mała. Zapisać?", "Weryfikacja", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No) return;
                }

                if (aktualnyTryb != "Avilog" && string.IsNullOrEmpty(cbOdbiorcy.Text))
                {
                    MessageBox.Show("Wybierz ODBIORCĘ!", "Brak danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                WybranaDostwa.Brutto = b;
                WybranaDostwa.Tara = t;
                WybranaDostwa.Netto = b - t;

                if (aktualnyTryb != "Avilog")
                {
                    WybranaDostwa.NrRejestracyjny = txtEditRejestracja.Text;
                    WybranaDostwa.HodowcaNazwa = cbOdbiorcy.Text;
                }

                WybranaDostwa.UpdateStatus();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "";

                    if (aktualnyTryb == "Avilog")
                    {
                        query = @"UPDATE dbo.FarmerCalc SET FullFarmWeight=@B, EmptyFarmWeight=@T, NettoFarmWeight=@N, FarmWeightDate=GETDATE(), FarmWeightUser='Portier', FarmWeightSource='PanelPortiera' WHERE ID=@ID";
                    }
                    else
                    {
                        if (WybranaDostwa.ID == -1)
                            query = @"INSERT INTO dbo.OdpadyRejestr (Towar, NrRejestracyjny, Odbiorca, DataWazenia, Brutto, Tara, Netto, Status)
                                      VALUES (@Towar, @Nr, @Odbiorca, GETDATE(), @B, @T, @N, 'Zakończone')";
                        else
                            query = @"UPDATE dbo.OdpadyRejestr SET Brutto=@B, Tara=@T, Netto=@N, NrRejestracyjny=@Nr, Odbiorca=@Odbiorca, Status='Zakończone', Towar=@Towar WHERE ID=@ID";
                    }

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@B", b);
                        cmd.Parameters.AddWithValue("@T", t);
                        cmd.Parameters.AddWithValue("@N", b - t);
                        cmd.Parameters.AddWithValue("@ID", WybranaDostwa.ID);

                        if (aktualnyTryb != "Avilog")
                        {
                            cmd.Parameters.AddWithValue("@Nr", WybranaDostwa.NrRejestracyjny);
                            cmd.Parameters.AddWithValue("@Towar", WybranaDostwa.Towar);
                            cmd.Parameters.AddWithValue("@Odbiorca", WybranaDostwa.HodowcaNazwa);
                        }

                        cmd.ExecuteNonQuery();
                    }
                }
                LoadDostawy();
                ClearWybranaDostwa();
            }
            catch (Exception ex) { MessageBox.Show("Błąd zapisu: " + ex.Message); }
        }

        private void BtnNewEntry_Click(object sender, RoutedEventArgs e)
        {
            var nowe = new DostawaPortiera
            {
                ID = -1,
                GodzinaPrzyjazdu = DateTime.Now.ToString("HH:mm"),
                Towar = "KREW", // Domyślnie
                NrRejestracyjny = "",
                HodowcaNazwa = "",
                Brutto = 0,
                Tara = 0,
                Netto = 0
            };
            dostawy.Add(nowe);
            gridTable.SelectedItem = nowe;
            WybierzDostawe(nowe);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (WybranaDostwa == null || WybranaDostwa.ID == -1)
            {
                dostawy.Remove(WybranaDostwa);
                ClearWybranaDostwa();
                return;
            }

            if (MessageBox.Show("Czy na pewno chcesz USUNĄĆ ten wpis?", "Usuwanie", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = "DELETE FROM dbo.OdpadyRejestr WHERE ID=@ID";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ID", WybranaDostwa.ID);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    LoadDostawy();
                    ClearWybranaDostwa();
                }
                catch (Exception ex) { MessageBox.Show("Błąd usuwania: " + ex.Message); }
            }
        }

        public void BtnDrukuj_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Drukowanie...");

        // === WAGA RHEWA ===
        private void ConnectToScale(string portName, int baudRate)
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen) serialPort.Close();
                serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();

                lblScaleStatus.Text = $"Połączono ({portName})";
                lblScaleStatus.Foreground = Brushes.LightGreen;
                ledStabilnosc.Fill = Brushes.LightGreen;
            }
            catch
            {
                lblScaleStatus.Text = "Waga Offline";
                lblScaleStatus.Foreground = Brushes.Gray;
                ledStabilnosc.Fill = Brushes.Gray;
            }
        }

        public void BtnReadScale_Click(object sender, RoutedEventArgs e)
        {
            if (serialPort != null && serialPort.IsOpen)
            {
                try { serialPort.Write("S"); }
                catch { MessageBox.Show("Błąd wysyłania komendy do wagi."); }
            }
            else MessageBox.Show("Waga nie jest podłączona.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = serialPort.ReadExisting();
                Match match = Regex.Match(data, @"\d+");
                if (match.Success && int.TryParse(match.Value, out int weight))
                {
                    if (weight > 0 && weight < 100000)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            TextBlock target = (aktywnePole == AktywnePole.Brutto) ? txtBrutto : txtTara;
                            target.Text = weight.ToString();
                            UpdateBigDisplay();
                        });
                    }
                }
            }
            catch { }
        }
    }

    public class DostawaPortiera : INotifyPropertyChanged
    {
        public long ID { get; set; }
        public string Lp { get; set; }
        public string HodowcaNazwa { get; set; }
        public string KierowcaNazwa { get; set; }
        public string CarID { get; set; }
        public string TrailerID { get; set; }
        public string NrRejestracyjny { get; set; }
        public int SztukiPlan { get; set; }
        public string GodzinaPrzyjazdu { get; set; }
        public string Towar { get; set; }

        private int _brutto;
        public int Brutto { get => _brutto; set { _brutto = value; NotifyAll(); } }

        private int _tara;
        public int Tara { get => _tara; set { _tara = value; NotifyAll(); } }

        private int _netto;
        public int Netto { get => _netto; set { _netto = value; NotifyAll(); } }

        public string BruttoDisplay => Brutto > 0 ? $"{Brutto:N0} kg" : "-";
        public string TaraDisplay => Tara > 0 ? $"{Tara:N0} kg" : "-";
        public string NettoDisplay => Netto > 0 ? $"{Netto:N0} kg" : "-";
        public string SztukiPlanDisplay => SztukiPlan > 0 ? $"{SztukiPlan:N0} szt" : "";
        public bool HasNetto => Netto > 0;
        public Visibility SztukiVisibility => SztukiPlan > 0 ? Visibility.Visible : Visibility.Collapsed;

        public SolidColorBrush StatusColor
        {
            get
            {
                if (Brutto > 0 && Tara > 0) return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#4CAF50")); // Zielony
                if (Brutto > 0 || Tara > 0) return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#FFA726")); // Pomarańczowy
                return new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#F44336")); // Czerwony
            }
        }

        public string StatusText => (Brutto > 0 && Tara > 0) ? "ZAKOŃCZONE" : ((Brutto > 0 || Tara > 0) ? "W TOKU" : "NOWE");

        public void UpdateStatus() => NotifyAll();
        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }
}