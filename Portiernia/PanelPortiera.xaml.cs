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
    /// Panel Portiera - dotykowy interfejs do rejestracji wag dostaw żywca
    /// Zapisuje wagi do tabeli FarmerCalc (FullFarmWeight, EmptyFarmWeight, NettoFarmWeight)
    /// </summary>
    public partial class PanelPortiera : Window
    {
        private string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private ObservableCollection<DostawaPortiera> dostawy;
        private DostawaPortiera wybranaDostwa;
        private DateTime selectedDate = DateTime.Today;

        // Aktywne pole wprowadzania (Brutto lub Tara)
        private enum AktywnePole { Brutto, Tara }
        private AktywnePole aktywnePole = AktywnePole.Brutto;

        public PanelPortiera()
        {
            InitializeComponent();
            dostawy = new ObservableCollection<DostawaPortiera>();
            listDostawy.ItemsSource = dostawy;
        }

        public PanelPortiera(DateTime data) : this()
        {
            selectedDate = data;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateDateDisplay();
            LoadDostawy();
            UpdateStatistics();

            // Domyślnie aktywne pole Brutto
            SetAktywnePole(AktywnePole.Brutto);
        }

        #region Ładowanie danych z FarmerCalc

        private void LoadDostawy()
        {
            try
            {
                dostawy.Clear();

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz dostawy z FarmerCalc dla wybranej daty
                    string query = @"
                        SELECT 
                            fc.ID,
                            fc.LpDostawy,
                            fc.CustomerGID,
                            (SELECT TOP 1 ShortName FROM dbo.Dostawcy WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(fc.CustomerGID))) as HodowcaNazwa,
                            fc.DriverGID,
                            ISNULL(dr.[Name], '') as KierowcaNazwa,
                            fc.CarID,
                            fc.TrailerID,
                            fc.Wyjazd,
                            fc.Zaladunek,
                            fc.Przyjazd,
                            fc.SztPoj,
                            fc.WagaDek,
                            ISNULL(fc.FullFarmWeight, 0) as BruttoHodowcy,
                            ISNULL(fc.EmptyFarmWeight, 0) as TaraHodowcy,
                            ISNULL(fc.NettoFarmWeight, 0) as NettoHodowcy,
                            fc.NotkaWozek
                        FROM dbo.FarmerCalc fc
                        LEFT JOIN dbo.Driver dr ON fc.DriverGID = dr.GID
                        WHERE CAST(fc.CalcDate AS DATE) = @Data
                        ORDER BY fc.ID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Data", selectedDate.Date);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            int lp = 1;
                            while (reader.Read())
                            {
                                string customerGID = reader["CustomerGID"]?.ToString()?.Trim() ?? "";
                                string hodowcaNazwa = reader["HodowcaNazwa"]?.ToString() ?? "";

                                // Jeśli brak nazwy hodowcy
                                if (string.IsNullOrEmpty(hodowcaNazwa))
                                {
                                    hodowcaNazwa = customerGID == "-1" ? "Do przypisania" : "Nieznany";
                                }

                                var dostawa = new DostawaPortiera
                                {
                                    ID = Convert.ToInt64(reader["ID"]),
                                    Lp = lp.ToString(),
                                    LpDostawy = reader["LpDostawy"]?.ToString() ?? lp.ToString(),
                                    CustomerGID = customerGID,
                                    HodowcaNazwa = hodowcaNazwa,
                                    DriverGID = reader["DriverGID"] != DBNull.Value ? Convert.ToInt32(reader["DriverGID"]) : (int?)null,
                                    KierowcaNazwa = reader["KierowcaNazwa"]?.ToString() ?? "",
                                    CarID = reader["CarID"]?.ToString() ?? "",
                                    TrailerID = reader["TrailerID"]?.ToString() ?? "",
                                    Uwagi = reader["NotkaWozek"]?.ToString() ?? ""
                                };

                                // Numer rejestracyjny
                                dostawa.NrRejestracyjny = !string.IsNullOrEmpty(dostawa.CarID) ? dostawa.CarID : "-";
                                if (!string.IsNullOrEmpty(dostawa.TrailerID))
                                    dostawa.NrRejestracyjny += " / " + dostawa.TrailerID;

                                // Godziny
                                if (reader["Zaladunek"] != DBNull.Value)
                                    dostawa.GodzinaZaladunku = Convert.ToDateTime(reader["Zaladunek"]);
                                if (reader["Przyjazd"] != DBNull.Value)
                                    dostawa.GodzinaPrzyjazdu = Convert.ToDateTime(reader["Przyjazd"]);
                                if (reader["Wyjazd"] != DBNull.Value)
                                    dostawa.GodzinaWyjazdu = Convert.ToDateTime(reader["Wyjazd"]);

                                // Sztuki i waga deklarowana (decimal w bazie)
                                dostawa.SztukiPlan = reader["SztPoj"] != DBNull.Value ? Convert.ToInt32(Convert.ToDecimal(reader["SztPoj"])) : 0;
                                dostawa.WagaDek = reader["WagaDek"] != DBNull.Value ? Convert.ToDecimal(reader["WagaDek"]) : 0;

                                // Wagi od hodowcy (z FarmerCalc) - decimal w bazie
                                dostawa.Brutto = reader["BruttoHodowcy"] != DBNull.Value ? Convert.ToInt32(Convert.ToDecimal(reader["BruttoHodowcy"])) : 0;
                                dostawa.Tara = reader["TaraHodowcy"] != DBNull.Value ? Convert.ToInt32(Convert.ToDecimal(reader["TaraHodowcy"])) : 0;
                                dostawa.Netto = reader["NettoHodowcy"] != DBNull.Value ? Convert.ToInt32(Convert.ToDecimal(reader["NettoHodowcy"])) : 0;

                                // Status
                                dostawa.UpdateStatus();

                                dostawy.Add(dostawa);
                                lp++;
                            }
                        }
                    }
                }

                if (dostawy.Count == 0)
                {
                    // Brak danych - info w panelu
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania dostaw:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetHodowcaNazwa(SqlConnection conn, string customerGID)
        {
            if (string.IsNullOrWhiteSpace(customerGID))
                return "Nieznany";

            string trimmedGID = customerGID.Trim();

            // -1 oznacza brak przypisanego hodowcy
            if (trimmedGID == "-1")
                return "Do przypisania";

            try
            {
                // ID w Dostawcy to varchar - porównanie z TRIM
                string query = "SELECT ShortName FROM dbo.Dostawcy WHERE LTRIM(RTRIM(ID)) = @ID";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@ID", trimmedGID);
                    var result = cmd.ExecuteScalar();
                    return result?.ToString() ?? "Nieznany";
                }
            }
            catch
            {
                return "Nieznany";
            }
        }

        #endregion

        #region Obsługa UI

        private void UpdateDateDisplay()
        {
            lblData.Text = selectedDate.ToString("dd.MM.yyyy");
            var culture = new CultureInfo("pl-PL");
            string dayName = selectedDate.ToString("dddd", culture);
            lblDzienTygodnia.Text = char.ToUpper(dayName[0]) + dayName.Substring(1);
        }

        private void UpdateStatistics()
        {
            lblLiczbaDostaw.Text = dostawy.Count.ToString();
            lblOczekuje.Text = dostawy.Count(d => d.Status == StatusDostawy.Oczekuje).ToString();
            lblZwazone.Text = dostawy.Count(d => d.Status == StatusDostawy.Zakonczony).ToString();

            int sumaNetto = dostawy.Sum(d => d.Netto);
            lblSumaNetto.Text = $"{sumaNetto:N0} kg";
        }

        private void BtnPreviousDay_Click(object sender, RoutedEventArgs e)
        {
            selectedDate = selectedDate.AddDays(-1);
            UpdateDateDisplay();
            LoadDostawy();
            UpdateStatistics();
            ClearWybranaDostwa();
        }

        private void BtnNextDay_Click(object sender, RoutedEventArgs e)
        {
            selectedDate = selectedDate.AddDays(1);
            UpdateDateDisplay();
            LoadDostawy();
            UpdateStatistics();
            ClearWybranaDostwa();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDostawy();
            UpdateStatistics();

            // Odśwież wybraną dostawę jeśli istnieje
            if (wybranaDostwa != null)
            {
                var odswiezona = dostawy.FirstOrDefault(d => d.ID == wybranaDostwa.ID);
                if (odswiezona != null)
                {
                    WybierzDostawe(odswiezona);
                }
                else
                {
                    ClearWybranaDostwa();
                }
            }
        }

        private void Dostawa_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is DostawaPortiera dostawa)
            {
                WybierzDostawe(dostawa);
            }
        }

        private void WybierzDostawe(DostawaPortiera dostawa)
        {
            wybranaDostwa = dostawa;

            // Aktualizuj panel
            lblWybranyLp.Text = $"LP: {dostawa.Lp}";
            lblWybranyHodowca.Text = dostawa.HodowcaNazwa;
            lblWybranyKierowca.Text = $"🚚 {dostawa.KierowcaNazwa} • {dostawa.NrRejestracyjny}";

            // Wczytaj zapisane wagi
            txtBrutto.Text = dostawa.Brutto > 0 ? dostawa.Brutto.ToString() : "0";
            txtTara.Text = dostawa.Tara > 0 ? dostawa.Tara.ToString() : "0";
            UpdateNetto();

            // Włącz przycisk zapisu
            btnZapisz.IsEnabled = true;

            // Ustaw aktywne pole na Brutto
            SetAktywnePole(AktywnePole.Brutto);
        }

        private void ClearWybranaDostwa()
        {
            wybranaDostwa = null;
            lblWybranyLp.Text = "LP: -";
            lblWybranyHodowca.Text = "Wybierz dostawę z listy";
            lblWybranyKierowca.Text = "";
            txtBrutto.Text = "0";
            txtTara.Text = "0";
            txtNetto.Text = "0 kg";
            btnZapisz.IsEnabled = false;
        }

        #endregion

        #region Klawiatura numeryczna

        private void SetAktywnePole(AktywnePole pole)
        {
            aktywnePole = pole;

            // Podświetl aktywne pole
            if (pole == AktywnePole.Brutto)
            {
                borderBrutto.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA726"));
                borderTara.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F3460"));
            }
            else
            {
                borderBrutto.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F3460"));
                borderTara.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#42A5F5"));
            }
        }

        private void TxtBrutto_Click(object sender, MouseButtonEventArgs e)
        {
            SetAktywnePole(AktywnePole.Brutto);
        }

        private void TxtTara_Click(object sender, MouseButtonEventArgs e)
        {
            SetAktywnePole(AktywnePole.Tara);
        }

        private void NumpadClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                string digit = btn.Content.ToString();

                TextBlock targetField = aktywnePole == AktywnePole.Brutto ? txtBrutto : txtTara;

                string currentValue = targetField.Text;
                if (currentValue == "0")
                    currentValue = "";

                // Limit do 6 cyfr (max 999999 kg)
                if (currentValue.Length < 6)
                {
                    currentValue += digit;
                    targetField.Text = currentValue;
                    UpdateNetto();
                }
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TextBlock targetField = aktywnePole == AktywnePole.Brutto ? txtBrutto : txtTara;
            targetField.Text = "0";
            UpdateNetto();
        }

        private void BtnBackspace_Click(object sender, RoutedEventArgs e)
        {
            TextBlock targetField = aktywnePole == AktywnePole.Brutto ? txtBrutto : txtTara;
            string currentValue = targetField.Text;

            if (currentValue.Length > 1)
            {
                targetField.Text = currentValue.Substring(0, currentValue.Length - 1);
            }
            else
            {
                targetField.Text = "0";
            }

            UpdateNetto();
        }

        private void UpdateNetto()
        {
            int brutto = 0;
            int tara = 0;

            int.TryParse(txtBrutto.Text, out brutto);
            int.TryParse(txtTara.Text, out tara);

            int netto = brutto - tara;
            txtNetto.Text = $"{netto:N0} kg";
        }

        #endregion

        #region Zapis do FarmerCalc

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (wybranaDostwa == null)
            {
                MessageBox.Show("Wybierz dostawę z listy!", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int brutto = 0;
            int tara = 0;

            if (!int.TryParse(txtBrutto.Text, out brutto) || brutto <= 0)
            {
                MessageBox.Show("Wprowadź poprawną wagę BRUTTO!", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                SetAktywnePole(AktywnePole.Brutto);
                return;
            }

            int.TryParse(txtTara.Text, out tara);
            int netto = brutto - tara;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Zapisz wagi od hodowcy do FarmerCalc
                    // FullFarmWeight = Brutto
                    // EmptyFarmWeight = Tara  
                    // NettoFarmWeight = Netto
                    string updateQuery = @"
                        UPDATE dbo.FarmerCalc 
                        SET FullFarmWeight = @Brutto,
                            EmptyFarmWeight = @Tara,
                            NettoFarmWeight = @Netto
                        WHERE ID = @ID";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Brutto", brutto);
                        cmd.Parameters.AddWithValue("@Tara", tara);
                        cmd.Parameters.AddWithValue("@Netto", netto);
                        cmd.Parameters.AddWithValue("@ID", wybranaDostwa.ID);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Aktualizuj lokalnie
                            wybranaDostwa.Brutto = brutto;
                            wybranaDostwa.Tara = tara;
                            wybranaDostwa.Netto = netto;
                            wybranaDostwa.UpdateStatus();

                            // Odśwież listę
                            listDostawy.Items.Refresh();
                            UpdateStatistics();

                            // Pokaż krótkie potwierdzenie
                            ShowSuccessMessage($"✓ {wybranaDostwa.HodowcaNazwa}\nNetto: {netto:N0} kg");

                            // Przejdź do następnej dostawy oczekującej
                            var nastepna = dostawy.FirstOrDefault(d =>
                                d.Status == StatusDostawy.Oczekuje && d.ID != wybranaDostwa.ID);

                            if (nastepna != null)
                            {
                                WybierzDostawe(nastepna);
                            }
                            else
                            {
                                // Wszystkie zważone!
                                if (dostawy.All(d => d.Status == StatusDostawy.Zakonczony))
                                {
                                    MessageBox.Show("Wszystkie dostawy zostały zważone! 🎉",
                                        "Gratulacje!", MessageBoxButton.OK, MessageBoxImage.Information);
                                }
                                ClearWybranaDostwa();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu wagi:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowSuccessMessage(string message)
        {
            // Krótki komunikat sukcesu
            MessageBox.Show(message, "Zapisano", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
    }

    #region Klasy pomocnicze

    /// <summary>
    /// Status dostawy
    /// </summary>
    public enum StatusDostawy
    {
        Oczekuje,
        WTrakcie,
        Zakonczony
    }

    /// <summary>
    /// Model dostawy dla panelu portiera
    /// </summary>
    public class DostawaPortiera : INotifyPropertyChanged
    {
        public long ID { get; set; }
        public string Lp { get; set; }
        public string LpDostawy { get; set; }
        public string CustomerGID { get; set; }
        public string HodowcaNazwa { get; set; }
        public int? DriverGID { get; set; }
        public string KierowcaNazwa { get; set; }
        public string CarID { get; set; }
        public string TrailerID { get; set; }
        public string NrRejestracyjny { get; set; }
        public string Uwagi { get; set; }

        public DateTime? GodzinaWyjazdu { get; set; }
        public DateTime? GodzinaZaladunku { get; set; }
        public DateTime? GodzinaPrzyjazdu { get; set; }

        public int SztukiPlan { get; set; }
        public decimal WagaDek { get; set; }

        private int _brutto;
        public int Brutto
        {
            get => _brutto;
            set { _brutto = value; OnPropertyChanged(nameof(Brutto)); OnPropertyChanged(nameof(BruttoDisplay)); }
        }

        private int _tara;
        public int Tara
        {
            get => _tara;
            set { _tara = value; OnPropertyChanged(nameof(Tara)); OnPropertyChanged(nameof(TaraDisplay)); }
        }

        private int _netto;
        public int Netto
        {
            get => _netto;
            set { _netto = value; OnPropertyChanged(nameof(Netto)); OnPropertyChanged(nameof(NettoDisplay)); }
        }

        private StatusDostawy _status = StatusDostawy.Oczekuje;
        public StatusDostawy Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        // Wyświetlanie
        public string BruttoDisplay => Brutto > 0 ? $"{Brutto:N0}" : "-";
        public string TaraDisplay => Tara > 0 ? $"{Tara:N0}" : "-";
        public string NettoDisplay => Netto > 0 ? $"{Netto:N0}" : "-";

        public string StatusText
        {
            get
            {
                switch (Status)
                {
                    case StatusDostawy.Oczekuje: return "OCZEKUJE";
                    case StatusDostawy.WTrakcie: return "W TRAKCIE";
                    case StatusDostawy.Zakonczony: return "ZWAŻONY";
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
                    case StatusDostawy.Oczekuje: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFA726"));
                    case StatusDostawy.WTrakcie: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#42A5F5"));
                    case StatusDostawy.Zakonczony: return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#66BB6A"));
                    default: return new SolidColorBrush(Colors.Gray);
                }
            }
        }

        public void UpdateStatus()
        {
            if (Netto > 0)
                Status = StatusDostawy.Zakonczony;
            else if (Brutto > 0)
                Status = StatusDostawy.WTrakcie;
            else
                Status = StatusDostawy.Oczekuje;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    #endregion
}