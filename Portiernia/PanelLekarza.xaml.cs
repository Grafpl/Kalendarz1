using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1
{
    /// <summary>
    /// Panel Lekarza - dotykowy interfejs do oceny dobrostanu drobiu
    /// Zapisuje oceny do tabeli FarmerCalc (DeclI2-DeclI5 + Lapki)
    /// </summary>
    public partial class PanelLekarza : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        
        private ObservableCollection<DostawaLekarza> dostawy;
        private DostawaLekarza wybranaDostwa;
        private DateTime selectedDate = DateTime.Today;
        
        // Aktywne pole wprowadzania
        private enum AktywnePole { Padle, CH, NW, ZM }
        private AktywnePole aktywnePole = AktywnePole.Padle;

        // Źródło zapisu
        private const string ASSESSMENT_SOURCE = "PanelLekarza";

        // Timery
        private System.Windows.Threading.DispatcherTimer autoRefreshTimer;
        private System.Windows.Threading.DispatcherTimer dateCheckTimer;

        public PanelLekarza()
        {
            InitializeComponent();
            dostawy = new ObservableCollection<DostawaLekarza>();
            listDostawy.ItemsSource = dostawy;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDateDisplay();
            LoadDostawy();
            
            // Domyślnie aktywne pole Padłe
            SetAktywnePole(AktywnePole.Padle);

            // Timer auto-odświeżania co 5 minut
            autoRefreshTimer = new System.Windows.Threading.DispatcherTimer();
            autoRefreshTimer.Interval = TimeSpan.FromMinutes(5);
            autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
            autoRefreshTimer.Start();

            // Timer sprawdzania daty co godzinę
            dateCheckTimer = new System.Windows.Threading.DispatcherTimer();
            dateCheckTimer.Interval = TimeSpan.FromHours(1);
            dateCheckTimer.Tick += DateCheckTimer_Tick;
            dateCheckTimer.Start();
        }

        private void AutoRefreshTimer_Tick(object sender, EventArgs e)
        {
            LoadDostawy();
            
            if (wybranaDostwa != null)
            {
                var odswiezona = dostawy.FirstOrDefault(d => d.ID == wybranaDostwa.ID);
                if (odswiezona != null)
                {
                    WybierzDostawe(odswiezona);
                }
            }
        }

        private void DateCheckTimer_Tick(object sender, EventArgs e)
        {
            if (selectedDate.Date != DateTime.Today)
            {
                selectedDate = DateTime.Today;
                UpdateDateDisplay();
                LoadDostawy();
                ClearWybranaDostwa();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            autoRefreshTimer?.Stop();
            dateCheckTimer?.Stop();
            base.OnClosed(e);
        }

        #region Nawigacja dat

        private void UpdateDateDisplay()
        {
            var culture = new CultureInfo("pl-PL");
            lblData.Text = selectedDate.ToString("dd MMMM yyyy", culture).ToUpper();
            lblDzien.Text = selectedDate.ToString("dddd", culture).ToUpper();
        }

        private void BtnPreviousDay_Click(object sender, RoutedEventArgs e)
        {
            selectedDate = selectedDate.AddDays(-1);
            UpdateDateDisplay();
            LoadDostawy();
            ClearWybranaDostwa();
        }

        private void BtnNextDay_Click(object sender, RoutedEventArgs e)
        {
            selectedDate = selectedDate.AddDays(1);
            UpdateDateDisplay();
            LoadDostawy();
            ClearWybranaDostwa();
        }

        #endregion

        #region Ładowanie danych

        private void LoadDostawy()
        {
            try
            {
                dostawy.Clear();

                string query = @"
                    SELECT 
                        fc.ID,
                        fc.CarLp,
                        fc.CustomerGID,
                        fc.DeclI1 as SztukiDek,
                        fc.DeclI2 as Padle,
                        fc.DeclI3 as CH,
                        fc.DeclI4 as NW,
                        fc.DeclI5 as ZM,
                        fc.NettoFarmWeight,
                        fc.LumQnt as LUMEL
                    FROM FarmerCalc fc
                    WHERE fc.DayD = @Data
                    ORDER BY 
                        CASE WHEN ISNUMERIC(fc.LpDostawy) = 1 THEN CAST(fc.LpDostawy AS INT) ELSE 999999 END,
                        fc.ID";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Data", selectedDate.Date);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            int lp = 1;
                            while (reader.Read())
                            {
                                var dostawa = new DostawaLekarza
                                {
                                    ID = reader.GetInt64(reader.GetOrdinal("ID")),
                                    Lp = lp.ToString(),
                                    CustomerGID = reader.IsDBNull(reader.GetOrdinal("CustomerGID")) ? null : reader.GetString(reader.GetOrdinal("CustomerGID")),
                                    SztukiDek = reader.IsDBNull(reader.GetOrdinal("SztukiDek")) ? 0 : reader.GetInt32(reader.GetOrdinal("SztukiDek")),
                                    Padle = reader.IsDBNull(reader.GetOrdinal("Padle")) ? 0 : reader.GetInt32(reader.GetOrdinal("Padle")),
                                    CH = reader.IsDBNull(reader.GetOrdinal("CH")) ? 0 : reader.GetInt32(reader.GetOrdinal("CH")),
                                    NW = reader.IsDBNull(reader.GetOrdinal("NW")) ? 0 : reader.GetInt32(reader.GetOrdinal("NW")),
                                    ZM = reader.IsDBNull(reader.GetOrdinal("ZM")) ? 0 : reader.GetInt32(reader.GetOrdinal("ZM")),
                                    Netto = reader.IsDBNull(reader.GetOrdinal("NettoFarmWeight")) ? 0 : Convert.ToInt32(reader.GetDecimal(reader.GetOrdinal("NettoFarmWeight"))),
                                    LUMEL = reader.IsDBNull(reader.GetOrdinal("LUMEL")) ? 0 : reader.GetInt32(reader.GetOrdinal("LUMEL"))
                                };

                                // Pobierz nazwę hodowcy
                                dostawa.HodowcaNazwa = GetHodowcaNazwa(dostawa.CustomerGID);
                                dostawa.UpdateStatus();
                                
                                dostawy.Add(dostawa);
                                lp++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetHodowcaNazwa(string customerGID)
        {
            if (string.IsNullOrEmpty(customerGID) || customerGID == "-1")
                return "Nieprzypisany";

            try
            {
                string query = "SELECT ShortName FROM [LibraNet].[dbo].[Kontrahent] WHERE GID = @GID";
                
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@GID", customerGID);
                        var result = cmd.ExecuteScalar();
                        return result?.ToString() ?? "Nieznany";
                    }
                }
            }
            catch
            {
                return "Błąd";
            }
        }

        #endregion

        #region Wybór dostawy i pól

        private void Dostawa_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is DostawaLekarza dostawa)
            {
                WybierzDostawe(dostawa);
            }
        }

        private void WybierzDostawe(DostawaLekarza dostawa)
        {
            wybranaDostwa = dostawa;
            
            // Aktualizuj panel
            lblWybranyLp.Text = $"LP: {dostawa.Lp}";
            lblWybranyHodowca.Text = dostawa.HodowcaNazwa;
            lblSztukiNetto.Text = $"Sztuki: {dostawa.SztukiDek:N0} | LUMEL: {dostawa.LUMEL:N0} | Netto: {dostawa.Netto:N0} kg";
            
            // Wczytaj zapisane wartości
            txtPadle.Text = dostawa.Padle.ToString();
            txtCH.Text = dostawa.CH.ToString();
            txtNW.Text = dostawa.NW.ToString();
            txtZM.Text = dostawa.ZM.ToString();
            
            UpdateSumaKonfiskat();
            
            // Włącz przycisk zapisu
            btnZapisz.IsEnabled = true;
            
            // Ustaw aktywne pole na Padłe
            SetAktywnePole(AktywnePole.Padle);
        }

        private void ClearWybranaDostwa()
        {
            wybranaDostwa = null;
            lblWybranyLp.Text = "LP: -";
            lblWybranyHodowca.Text = "Wybierz dostawę z listy";
            lblSztukiNetto.Text = "";
            lblStatus.Text = "";
            txtPadle.Text = "0";
            txtCH.Text = "0";
            txtNW.Text = "0";
            txtZM.Text = "0";
            txtSumaKonfiskat.Text = "0 szt";
            btnZapisz.IsEnabled = false;
        }

        private void FieldClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is string fieldName)
            {
                switch (fieldName)
                {
                    case "Padle": SetAktywnePole(AktywnePole.Padle); break;
                    case "CH": SetAktywnePole(AktywnePole.CH); break;
                    case "NW": SetAktywnePole(AktywnePole.NW); break;
                    case "ZM": SetAktywnePole(AktywnePole.ZM); break;
                }
            }
        }

        private void SetAktywnePole(AktywnePole pole)
        {
            aktywnePole = pole;
            
            // Reset wszystkich borderów
            borderPadle.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F3460"));
            borderCH.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F3460"));
            borderNW.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F3460"));
            borderZM.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F3460"));
            
            // Podświetl aktywne pole
            switch (pole)
            {
                case AktywnePole.Padle:
                    borderPadle.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E94560"));
                    lblAktywnePole.Text = "Wprowadzasz: PADŁE";
                    break;
                case AktywnePole.CH:
                    borderCH.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA726"));
                    lblAktywnePole.Text = "Wprowadzasz: CH (chód)";
                    break;
                case AktywnePole.NW:
                    borderNW.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA726"));
                    lblAktywnePole.Text = "Wprowadzasz: NW (niedowaga)";
                    break;
                case AktywnePole.ZM:
                    borderZM.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA726"));
                    lblAktywnePole.Text = "Wprowadzasz: ZM (zmiany)";
                    break;
            }
        }

        #endregion

        #region Numpad

        private void NumpadClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string digit = btn.Content.ToString();
                AppendToActiveField(digit);
            }
        }

        private void AppendToActiveField(string digit)
        {
            TextBlock activeTextBlock = GetActiveTextBlock();
            if (activeTextBlock != null)
            {
                string currentValue = activeTextBlock.Text;
                
                // Jeśli aktualna wartość to "0", zastąp ją
                if (currentValue == "0")
                {
                    activeTextBlock.Text = digit;
                }
                else
                {
                    // Ogranicz do 6 cyfr
                    if (currentValue.Length < 6)
                    {
                        activeTextBlock.Text = currentValue + digit;
                    }
                }
                
                UpdateSumaKonfiskat();
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TextBlock activeTextBlock = GetActiveTextBlock();
            if (activeTextBlock != null)
            {
                activeTextBlock.Text = "0";
                UpdateSumaKonfiskat();
            }
        }

        private void BtnBackspace_Click(object sender, RoutedEventArgs e)
        {
            TextBlock activeTextBlock = GetActiveTextBlock();
            if (activeTextBlock != null)
            {
                string currentValue = activeTextBlock.Text;
                if (currentValue.Length > 1)
                {
                    activeTextBlock.Text = currentValue.Substring(0, currentValue.Length - 1);
                }
                else
                {
                    activeTextBlock.Text = "0";
                }
                UpdateSumaKonfiskat();
            }
        }

        private TextBlock GetActiveTextBlock()
        {
            switch (aktywnePole)
            {
                case AktywnePole.Padle: return txtPadle;
                case AktywnePole.CH: return txtCH;
                case AktywnePole.NW: return txtNW;
                case AktywnePole.ZM: return txtZM;
                default: return null;
            }
        }

        private void UpdateSumaKonfiskat()
        {
            int ch = int.TryParse(txtCH.Text, out int chVal) ? chVal : 0;
            int nw = int.TryParse(txtNW.Text, out int nwVal) ? nwVal : 0;
            int zm = int.TryParse(txtZM.Text, out int zmVal) ? zmVal : 0;
            
            int suma = ch + nw + zm;
            txtSumaKonfiskat.Text = $"{suma:N0} szt";
        }

        #endregion

        #region Zapis danych

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (wybranaDostwa == null)
            {
                MessageBox.Show("Wybierz dostawę!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pobierz wartości
            if (!int.TryParse(txtPadle.Text, out int padle)) padle = 0;
            if (!int.TryParse(txtCH.Text, out int ch)) ch = 0;
            if (!int.TryParse(txtNW.Text, out int nw)) nw = 0;
            if (!int.TryParse(txtZM.Text, out int zm)) zm = 0;

            try
            {
                string userName = App.UserFullName ?? App.UserID ?? "Lekarz";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    
                    string updateQuery = @"
                        UPDATE FarmerCalc SET
                            DeclI2 = @Padle,
                            DeclI3 = @CH,
                            DeclI4 = @NW,
                            DeclI5 = @ZM
                        WHERE ID = @ID";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Padle", padle);
                        cmd.Parameters.AddWithValue("@CH", ch);
                        cmd.Parameters.AddWithValue("@NW", nw);
                        cmd.Parameters.AddWithValue("@ZM", zm);
                        cmd.Parameters.AddWithValue("@ID", wybranaDostwa.ID);

                        int rowsAffected = cmd.ExecuteNonQuery();
                        
                        if (rowsAffected > 0)
                        {
                            // Aktualizuj lokalnie
                            wybranaDostwa.Padle = padle;
                            wybranaDostwa.CH = ch;
                            wybranaDostwa.NW = nw;
                            wybranaDostwa.ZM = zm;
                            wybranaDostwa.UpdateStatus();

                            // Odśwież listę
                            listDostawy.Items.Refresh();

                            // Przejdź do następnej dostawy nieocenionej
                            var nastepna = dostawy.FirstOrDefault(d => 
                                d.Status == StatusOceny.Nieoceniona && d.ID != wybranaDostwa.ID);
                            
                            if (nastepna != null)
                            {
                                WybierzDostawe(nastepna);
                            }
                            else
                            {
                                MessageBox.Show("Zapisano ocenę!\nWszystkie dostawy zostały ocenione.", 
                                    "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu oceny:\n{ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }

    #region Klasy pomocnicze

    /// <summary>
    /// Status oceny dobrostanu
    /// </summary>
    public enum StatusOceny
    {
        Nieoceniona,
        CzesciowaOcena,
        Oceniona
    }

    /// <summary>
    /// Model dostawy dla panelu lekarza
    /// </summary>
    public class DostawaLekarza : INotifyPropertyChanged
    {
        public long ID { get; set; }
        public string Lp { get; set; }
        public string CustomerGID { get; set; }
        public string HodowcaNazwa { get; set; }
        public int SztukiDek { get; set; }
        public int Netto { get; set; }
        public int LUMEL { get; set; }
        
        private int _padle;
        private int _ch;
        private int _nw;
        private int _zm;
        private StatusOceny _status;

        public int Padle
        {
            get => _padle;
            set { _padle = value; OnPropertyChanged(nameof(Padle)); OnPropertyChanged(nameof(PadleDisplay)); }
        }

        public int CH
        {
            get => _ch;
            set { _ch = value; OnPropertyChanged(nameof(CH)); OnPropertyChanged(nameof(CHDisplay)); }
        }

        public int NW
        {
            get => _nw;
            set { _nw = value; OnPropertyChanged(nameof(NW)); OnPropertyChanged(nameof(NWDisplay)); }
        }

        public int ZM
        {
            get => _zm;
            set { _zm = value; OnPropertyChanged(nameof(ZM)); OnPropertyChanged(nameof(ZMDisplay)); }
        }

        public StatusOceny Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        // Właściwości wyświetlania
        public string SztukiInfo => $"{SztukiDek:N0} szt / LUMEL: {LUMEL:N0}";
        public string NettoDisplay => Netto > 0 ? $"{Netto:N0}" : "-";
        public string PadleDisplay => Padle > 0 ? Padle.ToString() : "-";
        public string CHDisplay => CH > 0 ? CH.ToString() : "-";
        public string NWDisplay => NW > 0 ? NW.ToString() : "-";
        public string ZMDisplay => ZM > 0 ? ZM.ToString() : "-";

        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case StatusOceny.Nieoceniona: return "CZEKA";
                    case StatusOceny.CzesciowaOcena: return "CZĘŚC.";
                    case StatusOceny.Oceniona: return "OK";
                    default: return "";
                }
            }
        }

        public SolidColorBrush StatusColor
        {
            get
            {
                switch (Status)
                {
                    case StatusOceny.Nieoceniona: return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA726"));
                    case StatusOceny.CzesciowaOcena: return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#42A5F5"));
                    case StatusOceny.Oceniona: return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#66BB6A"));
                    default: return new SolidColorBrush(Colors.Gray);
                }
            }
        }

        public void UpdateStatus()
        {
            // Sprawdź czy wszystkie wartości są wprowadzone
            // Ocena jest kompletna gdy mamy Padłe LUB jakiekolwiek konfiskaty
            bool maPadle = Padle > 0;
            bool maKonfiskaty = CH > 0 || NW > 0 || ZM > 0;
            
            if (maPadle || maKonfiskaty)
                Status = StatusOceny.Oceniona;
            else
                Status = StatusOceny.Nieoceniona;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    #endregion
}
