using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
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

            // Obsługa klawiatury
            this.KeyDown += Window_KeyDown;

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

            // Automatycznie wybierz pierwszą dostawę
            if (dostawy.Count > 0)
            {
                WybierzDostawe(dostawy[0]);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Obsługa klawiatury numerycznej i zwykłej
            if (e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                string digit = (e.Key - Key.D0).ToString();
                AppendToActiveField(digit);
                e.Handled = true;
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                string digit = (e.Key - Key.NumPad0).ToString();
                AppendToActiveField(digit);
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                // Enter = Następne pole (auto-zapis już działa)
                BtnEnter_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Back)
            {
                // Backspace
                BtnBackspace_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete || e.Key == Key.C)
            {
                // Delete lub C = Clear
                BtnClear_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                // Tab = przejdź do następnego pola
                PrzejdzDoNastepnegoPola();
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                // Strzałka w górę - poprzednia dostawa
                PrzejdzDoPorzedniejDostawy();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                // Strzałka w dół - następna dostawa
                PrzejdzDoNastepnejDostawy();
                e.Handled = true;
            }
            else if (e.Key == Key.PageUp)
            {
                // Page Up - przesuń dostawę w górę
                BtnMoveUp_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.PageDown)
            {
                // Page Down - przesuń dostawę w dół
                BtnMoveDown_Click(sender, e);
                e.Handled = true;
            }
        }

        private void PrzejdzDoNastepnegoPola()
        {
            switch (aktywnePole)
            {
                case AktywnePole.Padle: SetAktywnePole(AktywnePole.CH); break;
                case AktywnePole.CH: SetAktywnePole(AktywnePole.NW); break;
                case AktywnePole.NW: SetAktywnePole(AktywnePole.ZM); break;
                case AktywnePole.ZM: SetAktywnePole(AktywnePole.Padle); break;
            }
        }

        private void PrzejdzDoNastepnejDostawy()
        {
            if (wybranaDostwa == null && dostawy.Count > 0)
            {
                WybierzDostawe(dostawy[0]);
                return;
            }

            int currentIndex = dostawy.IndexOf(wybranaDostwa);
            if (currentIndex < dostawy.Count - 1)
            {
                WybierzDostawe(dostawy[currentIndex + 1]);
            }
        }

        private void PrzejdzDoPorzedniejDostawy()
        {
            if (wybranaDostwa == null) return;

            int currentIndex = dostawy.IndexOf(wybranaDostwa);
            if (currentIndex > 0)
            {
                WybierzDostawe(dostawy[currentIndex - 1]);
            }
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
                        ISNULL(fc.CustomerGID, '') as CustomerGID,
                        (SELECT TOP 1 ShortName FROM dbo.Dostawcy WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(fc.CustomerGID))) as HodowcaNazwa,
                        (SELECT TOP 1 AnimNo FROM dbo.Dostawcy WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(fc.CustomerGID))) as AnimNo,
                        (SELECT TOP 1 ISNULL(Address, '') + ', ' + ISNULL(PostalCode, '') + ' ' + ISNULL(City, '') 
                         FROM dbo.Dostawcy WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(fc.CustomerGID))) as Adres,
                        (SELECT TOP 1 ID FROM dbo.Dostawcy WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(fc.CustomerGID))) as ZdatneID,
                        ISNULL(fc.CarID, '') as CarID,
                        ISNULL(fc.TrailerID, '') as TrailerID,
                        ISNULL(fc.DeclI1, 0) as SztukiDek,
                        ISNULL(fc.DeclI2, 0) as Padle,
                        ISNULL(fc.DeclI3, 0) as CH,
                        ISNULL(fc.DeclI4, 0) as NW,
                        ISNULL(fc.DeclI5, 0) as ZM,
                        ISNULL(fc.VetNo, '') as NrSwZdrowia,
                        ISNULL(fc.VetComment, '') as DataSalmonella,
                        ISNULL(fc.NettoFarmWeight, 0) as NettoFarmWeight,
                        ISNULL(fc.LumQnt, 0) as LUMEL
                    FROM FarmerCalc fc
                    WHERE fc.CalcDate = @Data
                    ORDER BY fc.CarLp, fc.ID";

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
                                    ID = reader["ID"] != DBNull.Value ? Convert.ToInt32(reader["ID"]) : 0,
                                    Lp = lp.ToString(),
                                    CustomerGID = reader["CustomerGID"] != DBNull.Value ? reader["CustomerGID"].ToString() : null,
                                    HodowcaNazwa = reader["HodowcaNazwa"] != DBNull.Value ? reader["HodowcaNazwa"].ToString() : "Nieprzypisany",
                                    AnimNo = reader["AnimNo"] != DBNull.Value ? reader["AnimNo"].ToString() : "",
                                    Adres = reader["Adres"] != DBNull.Value ? reader["Adres"].ToString().Trim() : "",
                                    ZdatneID = reader["ZdatneID"] != DBNull.Value ? reader["ZdatneID"].ToString().Trim() : "",
                                    CarID = reader["CarID"] != DBNull.Value ? reader["CarID"].ToString() : "",
                                    TrailerID = reader["TrailerID"] != DBNull.Value ? reader["TrailerID"].ToString() : "",
                                    SztukiDek = reader["SztukiDek"] != DBNull.Value ? Convert.ToInt32(reader["SztukiDek"]) : 0,
                                    Padle = reader["Padle"] != DBNull.Value ? Convert.ToInt32(reader["Padle"]) : 0,
                                    CH = reader["CH"] != DBNull.Value ? Convert.ToInt32(reader["CH"]) : 0,
                                    NW = reader["NW"] != DBNull.Value ? Convert.ToInt32(reader["NW"]) : 0,
                                    ZM = reader["ZM"] != DBNull.Value ? Convert.ToInt32(reader["ZM"]) : 0,
                                    NrSwZdrowia = reader["NrSwZdrowia"] != DBNull.Value ? reader["NrSwZdrowia"].ToString() : "",
                                    DataSalmonella = reader["DataSalmonella"] != DBNull.Value ? reader["DataSalmonella"].ToString() : "",
                                    Netto = reader["NettoFarmWeight"] != DBNull.Value ? Convert.ToInt32(Convert.ToDecimal(reader["NettoFarmWeight"])) : 0,
                                    LUMEL = reader["LUMEL"] != DBNull.Value ? Convert.ToInt32(reader["LUMEL"]) : 0
                                };

                                // Ustaw nazwę jeśli pusta
                                if (string.IsNullOrEmpty(dostawa.HodowcaNazwa) || dostawa.HodowcaNazwa == "Nieprzypisany")
                                {
                                    dostawa.HodowcaNazwa = string.IsNullOrEmpty(dostawa.CustomerGID) ? "Nieprzypisany" : $"ID: {dostawa.CustomerGID}";
                                }
                                
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
                string query = "SELECT ShortName FROM [LibraNet].[dbo].[Dostawcy] WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(@GID))";
                
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
            
            // Pola edytowalne
            txtHodowcaNazwa.Text = dostawa.HodowcaNazwa ?? "";
            txtAnimNo.Text = dostawa.AnimNo ?? "";
            txtAdres.Text = dostawa.Adres ?? "";
            txtCiagnikEdit.Text = dostawa.CarID ?? "";
            txtNaczepaEdit.Text = dostawa.TrailerID ?? "";
            txtDataSalmonella.Text = dostawa.DataSalmonella ?? "";
            txtNrSwZdrowia.Text = dostawa.NrSwZdrowia ?? "";
            
            lblSztukiNetto.Text = $"Szt: {dostawa.SztukiDek:N0} | ID: {dostawa.ZdatneID}";
            
            // Wczytaj zapisane wartości
            txtPadle.Text = dostawa.Padle.ToString();
            txtCH.Text = dostawa.CH.ToString();
            txtNW.Text = dostawa.NW.ToString();
            txtZM.Text = dostawa.ZM.ToString();
            
            UpdateSumaKonfiskat();
            
            // Ustaw aktywne pole na Padłe
            SetAktywnePole(AktywnePole.Padle);
        }

        private void ClearWybranaDostwa()
        {
            wybranaDostwa = null;
            lblWybranyLp.Text = "LP: -";
            txtHodowcaNazwa.Text = "";
            txtAnimNo.Text = "";
            txtAdres.Text = "";
            txtCiagnikEdit.Text = "";
            txtNaczepaEdit.Text = "";
            txtDataSalmonella.Text = "";
            txtNrSwZdrowia.Text = "";
            lblSztukiNetto.Text = "";
            lblStatus.Text = "";
            txtPadle.Text = "0";
            txtCH.Text = "0";
            txtNW.Text = "0";
            txtZM.Text = "0";
            txtSumaKonfiskat.Text = "0 szt";
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
            borderPadle.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3D3D3D"));
            borderCH.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3D3D3D"));
            borderNW.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3D3D3D"));
            borderZM.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3D3D3D"));
            
            // Podświetl aktywne pole
            switch (pole)
            {
                case AktywnePole.Padle:
                    borderPadle.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F44336"));
                    lblAktywnePole.Text = "Wprowadzasz: PADŁE";
                    break;
                case AktywnePole.CH:
                    borderCH.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50"));
                    lblAktywnePole.Text = "Wprowadzasz: CH (chód)";
                    break;
                case AktywnePole.NW:
                    borderNW.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50"));
                    lblAktywnePole.Text = "Wprowadzasz: NW (niedowaga)";
                    break;
                case AktywnePole.ZM:
                    borderZM.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50"));
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
                AutoZapiszOcene(); // Automatyczny zapis
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TextBlock activeTextBlock = GetActiveTextBlock();
            if (activeTextBlock != null)
            {
                activeTextBlock.Text = "0";
                UpdateSumaKonfiskat();
                AutoZapiszOcene(); // Automatyczny zapis
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
                AutoZapiszOcene(); // Automatyczny zapis
            }
        }

        /// <summary>
        /// Automatyczny zapis oceny do bazy (bez komunikatu)
        /// </summary>
        private void AutoZapiszOcene()
        {
            if (wybranaDostwa == null) return;

            try
            {
                int padle = int.Parse(txtPadle.Text);
                int ch = int.Parse(txtCH.Text);
                int nw = int.Parse(txtNW.Text);
                int zm = int.Parse(txtZM.Text);

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string updateQuery = @"
                        UPDATE FarmerCalc 
                        SET DeclI2 = @Padle, 
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
                        cmd.ExecuteNonQuery();
                    }
                }

                // Aktualizuj lokalny obiekt
                wybranaDostwa.Padle = padle;
                wybranaDostwa.CH = ch;
                wybranaDostwa.NW = nw;
                wybranaDostwa.ZM = zm;

                // Aktualizuj status na liście
                listDostawy.Items.Refresh();
            }
            catch
            {
                // Cichy błąd - nie pokazuj komunikatu przy każdym zapisie
            }
        }

        /// <summary>
        /// ENTER - Przejdź do następnego pola lub następnej dostawy
        /// </summary>
        private void BtnEnter_Click(object sender, RoutedEventArgs e)
        {
            if (wybranaDostwa == null)
            {
                // Jeśli nie wybrano dostawy, wybierz pierwszą
                if (dostawy.Count > 0)
                {
                    WybierzDostawe(dostawy[0]);
                }
                return;
            }

            // Przejdź do następnego pola
            switch (aktywnePole)
            {
                case AktywnePole.Padle:
                    SetAktywnePole(AktywnePole.CH);
                    break;
                case AktywnePole.CH:
                    SetAktywnePole(AktywnePole.NW);
                    break;
                case AktywnePole.NW:
                    SetAktywnePole(AktywnePole.ZM);
                    break;
                case AktywnePole.ZM:
                    // Przejdź do następnej dostawy
                    PrzejdzDoNastepnejDostawy();
                    break;
            }
        }

        /// <summary>
        /// Zapisuje ocenę do bazy - zwraca true jeśli sukces
        /// </summary>
        private bool ZapiszOcene()
        {
            if (wybranaDostwa == null) return false;

            // Pobierz wartości
            if (!int.TryParse(txtPadle.Text, out int padle)) padle = 0;
            if (!int.TryParse(txtCH.Text, out int ch)) ch = 0;
            if (!int.TryParse(txtNW.Text, out int nw)) nw = 0;
            if (!int.TryParse(txtZM.Text, out int zm)) zm = 0;

            try
            {
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
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu oceny:\n{ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return false;
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

            if (ZapiszOcene())
            {
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

        #endregion

        #region Zmiana kolejności dostaw

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (wybranaDostwa == null) return;

            int currentIndex = dostawy.IndexOf(wybranaDostwa);
            if (currentIndex <= 0) return; // Już jest na górze

            // Zamień miejscami z poprzednią
            dostawy.Move(currentIndex, currentIndex - 1);
            
            // Przenumeruj i zapisz
            PrzenumerujIZapiszKolejnosc();
            
            // Odśwież widok
            listDostawy.Items.Refresh();
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (wybranaDostwa == null) return;

            int currentIndex = dostawy.IndexOf(wybranaDostwa);
            if (currentIndex >= dostawy.Count - 1) return; // Już jest na dole

            // Zamień miejscami z następną
            dostawy.Move(currentIndex, currentIndex + 1);
            
            // Przenumeruj i zapisz
            PrzenumerujIZapiszKolejnosc();
            
            // Odśwież widok
            listDostawy.Items.Refresh();
        }

        private void PrzenumerujIZapiszKolejnosc()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    
                    for (int i = 0; i < dostawy.Count; i++)
                    {
                        int nowyLp = i + 1;
                        dostawy[i].Lp = nowyLp.ToString();
                        
                        // Zapisz do bazy
                        string updateQuery = "UPDATE FarmerCalc SET CarLp = @CarLp WHERE ID = @ID";
                        using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@CarLp", nowyLp);
                            cmd.Parameters.AddWithValue("@ID", dostawy[i].ID);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
                
                // Aktualizuj wyświetlany LP
                if (wybranaDostwa != null)
                {
                    lblWybranyLp.Text = $"LP: {wybranaDostwa.Lp}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu kolejności:\n{ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Zapis danych hodowcy

        private void BtnZapiszDaneHodowcy_Click(object sender, RoutedEventArgs e)
        {
            if (wybranaDostwa == null)
            {
                MessageBox.Show("Wybierz dostawę!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string nowaNazwa = txtHodowcaNazwa.Text.Trim();
                string staraNazwa = wybranaDostwa.HodowcaNazwa ?? "";
                bool nazwaZmieniona = nowaNazwa != staraNazwa;
                bool aktualizujWszystkie = false;

                // Jeśli nazwa się zmieniła, zapytaj o aktualizację wszystkich
                if (nazwaZmieniona && !string.IsNullOrEmpty(wybranaDostwa.CustomerGID))
                {
                    // Policz ile dostaw ma ten sam CustomerGID
                    int iloscDostaw = dostawy.Count(d => d.CustomerGID == wybranaDostwa.CustomerGID);
                    
                    if (iloscDostaw > 1)
                    {
                        var result = MessageBox.Show(
                            $"Nazwa hodowcy została zmieniona.\n\n" +
                            $"Stara nazwa: {staraNazwa}\n" +
                            $"Nowa nazwa: {nowaNazwa}\n\n" +
                            $"Znaleziono {iloscDostaw} dostaw od tego hodowcy.\n\n" +
                            $"Czy chcesz zaktualizować nazwę we WSZYSTKICH dostawach?",
                            "Aktualizacja nazwy hodowcy",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question);

                        if (result == MessageBoxResult.Cancel)
                            return;

                        aktualizujWszystkie = (result == MessageBoxResult.Yes);
                    }
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // 1. Zapisz CarID, TrailerID, VetNo, VetComment do FarmerCalc
                    string updateFarmerCalc = @"
                        UPDATE FarmerCalc 
                        SET CarID = @CarID, 
                            TrailerID = @TrailerID,
                            VetNo = @VetNo,
                            VetComment = @VetComment
                        WHERE ID = @ID";
                    
                    using (SqlCommand cmd = new SqlCommand(updateFarmerCalc, conn))
                    {
                        cmd.Parameters.AddWithValue("@CarID", txtCiagnikEdit.Text.Trim());
                        cmd.Parameters.AddWithValue("@TrailerID", txtNaczepaEdit.Text.Trim());
                        cmd.Parameters.AddWithValue("@VetNo", txtNrSwZdrowia.Text.Trim());
                        cmd.Parameters.AddWithValue("@VetComment", txtDataSalmonella.Text.Trim());
                        cmd.Parameters.AddWithValue("@ID", wybranaDostwa.ID);
                        cmd.ExecuteNonQuery();
                    }

                    // 2. Zapisz dane do tabeli Dostawcy (jeśli jest CustomerGID)
                    if (!string.IsNullOrEmpty(wybranaDostwa.CustomerGID))
                    {
                        string updateDostawcy = @"
                            UPDATE dbo.Dostawcy 
                            SET AnimNo = @AnimNo,
                                Address = @Address,
                                ShortName = @ShortName,
                                Name = @ShortName
                            WHERE LTRIM(RTRIM(ID)) = LTRIM(RTRIM(@CustomerGID))";
                        
                        using (SqlCommand cmd = new SqlCommand(updateDostawcy, conn))
                        {
                            cmd.Parameters.AddWithValue("@AnimNo", txtAnimNo.Text.Trim());
                            cmd.Parameters.AddWithValue("@Address", txtAdres.Text.Trim());
                            cmd.Parameters.AddWithValue("@ShortName", nowaNazwa);
                            cmd.Parameters.AddWithValue("@CustomerGID", wybranaDostwa.CustomerGID);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // Aktualizuj lokalny obiekt
                    wybranaDostwa.CarID = txtCiagnikEdit.Text.Trim();
                    wybranaDostwa.TrailerID = txtNaczepaEdit.Text.Trim();
                    wybranaDostwa.AnimNo = txtAnimNo.Text.Trim();
                    wybranaDostwa.Adres = txtAdres.Text.Trim();
                    wybranaDostwa.HodowcaNazwa = nowaNazwa;
                    wybranaDostwa.DataSalmonella = txtDataSalmonella.Text.Trim();
                    wybranaDostwa.NrSwZdrowia = txtNrSwZdrowia.Text.Trim();

                    // Jeśli użytkownik chce zaktualizować wszystkie dostawy
                    if (aktualizujWszystkie)
                    {
                        foreach (var d in dostawy.Where(d => d.CustomerGID == wybranaDostwa.CustomerGID))
                        {
                            d.HodowcaNazwa = nowaNazwa;
                            d.AnimNo = txtAnimNo.Text.Trim();
                            d.Adres = txtAdres.Text.Trim();
                        }
                    }

                    // Odśwież listę
                    listDostawy.Items.Refresh();

                    string msg = "Dane hodowcy zostały zapisane!";
                    if (aktualizujWszystkie)
                    {
                        int count = dostawy.Count(d => d.CustomerGID == wybranaDostwa.CustomerGID);
                        msg += $"\n\nZaktualizowano nazwę w {count} dostawach.";
                    }

                    MessageBox.Show(msg, "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu danych hodowcy:\n{ex.Message}", "Błąd", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Drukowanie raportu

        private void BtnDrukuj_Click(object sender, RoutedEventArgs e)
        {
            if (dostawy.Count == 0)
            {
                MessageBox.Show("Brak danych do wydruku!", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                PrintDocument printDoc = new PrintDocument();
                printDoc.PrintPage += PrintDoc_PrintPage;
                printDoc.DocumentName = $"Raport_Lekarza_{selectedDate:yyyy-MM-dd}";
                
                // Ustawienie orientacji poziomej
                printDoc.DefaultPageSettings.Landscape = true;

                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    printDoc.PrinterSettings.PrinterName = printDialog.PrintQueue.Name;
                    printDoc.Print();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd drukowania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            float pageWidth = e.PageBounds.Width;
            float pageHeight = e.PageBounds.Height;
            float leftMargin = 25;
            float rightMargin = pageWidth - 25;
            float tableWidth = rightMargin - leftMargin;
            float y = 30;

            // Czcionki
            System.Drawing.Font fontTitle = new System.Drawing.Font("Arial", 14, System.Drawing.FontStyle.Bold);
            System.Drawing.Font fontSubtitle = new System.Drawing.Font("Arial", 10);
            System.Drawing.Font fontHeader = new System.Drawing.Font("Arial", 7, System.Drawing.FontStyle.Bold);
            System.Drawing.Font fontData = new System.Drawing.Font("Arial", 8);
            System.Drawing.Font fontDataBold = new System.Drawing.Font("Arial", 8, System.Drawing.FontStyle.Bold);
            System.Drawing.Font fontSummary = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold);

            SolidBrush brushBlack = new SolidBrush(System.Drawing.Color.Black);
            SolidBrush brushGray = new SolidBrush(System.Drawing.Color.FromArgb(80, 80, 80));
            SolidBrush brushHeaderBg = new SolidBrush(System.Drawing.Color.FromArgb(230, 230, 230));
            System.Drawing.Pen penThick = new System.Drawing.Pen(System.Drawing.Color.Black, 1.5f);
            System.Drawing.Pen penNormal = new System.Drawing.Pen(System.Drawing.Color.Black, 0.5f);

            // ═══════════════════════════════════════════════════════════════
            // NAGŁÓWEK - STYL PŁACHTY
            // ═══════════════════════════════════════════════════════════════
            string[] dniTygodnia = { "NIEDZIELA", "PONIEDZIAŁEK", "WTOREK", "ŚRODA", "CZWARTEK", "PIĄTEK", "SOBOTA" };
            string dzienTygodnia = dniTygodnia[(int)selectedDate.DayOfWeek];
            
            // Tytuł z lewej - data i dzień
            g.DrawString($"S K U P", fontTitle, brushBlack, leftMargin, y);
            g.DrawString($"{selectedDate:dd}. GRU. {selectedDate:yyyy}", fontSubtitle, brushBlack, leftMargin + 80, y + 5);
            
            y += 35;

            // ═══════════════════════════════════════════════════════════════
            // TABELA - FORMAT PŁACHTY WETERYNARYJNEJ
            // ═══════════════════════════════════════════════════════════════
            
            // Kolumny: LP | POZ DZ | OD KOGO | DATA SALM | NR ŚW.ZDR | NR GOSP | ILOŚĆ | WPR AUTO | PADŁE | ZDATNE | CH | NW | ZM | RAZEM
            float[] colWidths = { 25, 40, 140, 55, 65, 85, 45, 65, 40, 55, 35, 35, 35, 40 };
            float[] colX = new float[colWidths.Length];
            colX[0] = leftMargin;
            for (int i = 1; i < colWidths.Length; i++)
            {
                colX[i] = colX[i - 1] + colWidths[i - 1];
            }

            // Nagłówki dwuwierszowe
            string[] headers1 = { "Lp", "POZ", "OD KOGO", "DATA BAD.", "NR ŚW.", "NR", "ILOŚĆ", "WPR", "PADŁE", "ZDATNE", "", "KONFISKATY", "", "" };
            string[] headers2 = { "", "DZ.", "", "SALMONELI", "ZDROWIA", "GOSPODARSTWA", "", "AUTO", "", "I + II", "CH", "NW", "ZM", "RAZEM" };

            // Nagłówek tabeli - tło
            float headerHeight = 35;
            g.FillRectangle(brushHeaderBg, leftMargin, y, tableWidth, headerHeight);
            g.DrawRectangle(penThick, leftMargin, y, tableWidth, headerHeight);

            // Tekst nagłówka - linia 1
            float headerTextY1 = y + 5;
            float headerTextY2 = y + 18;
            
            for (int i = 0; i < headers1.Length; i++)
            {
                if (i > 0)
                    g.DrawLine(penNormal, colX[i], y, colX[i], y + headerHeight);
                
                if (!string.IsNullOrEmpty(headers1[i]))
                {
                    SizeF textSize = g.MeasureString(headers1[i], fontHeader);
                    float textX = colX[i] + (colWidths[i] - textSize.Width) / 2;
                    g.DrawString(headers1[i], fontHeader, brushBlack, textX, headerTextY1);
                }
                if (!string.IsNullOrEmpty(headers2[i]))
                {
                    SizeF textSize = g.MeasureString(headers2[i], fontHeader);
                    float textX = colX[i] + (colWidths[i] - textSize.Width) / 2;
                    g.DrawString(headers2[i], fontHeader, brushBlack, textX, headerTextY2);
                }
            }
            
            // Linia pozioma w nagłówku KONFISKATY
            g.DrawLine(penNormal, colX[10], y + 17, colX[14], y + 17);
            
            y += headerHeight;

            // Dane wierszy
            float rowHeight = 22;
            int sumaPadle = 0, sumaCH = 0, sumaNW = 0, sumaZM = 0;
            int sumaIlosc = 0;

            foreach (var d in dostawy)
            {
                if (y > pageHeight - 80)
                    break;

                // Ramka wiersza
                g.DrawRectangle(penNormal, leftMargin, y, tableWidth, rowHeight);

                float textY = y + 5;

                // LP
                DrawCenteredText(g, d.Lp, fontDataBold, brushBlack, colX[0], colWidths[0], textY);

                // POZ DZ. (puste na razie)
                DrawCenteredText(g, "", fontData, brushGray, colX[1], colWidths[1], textY);

                // OD KOGO (nazwa hodowcy)
                string nazwa = d.HodowcaNazwa ?? "-";
                if (nazwa.Length > 22) nazwa = nazwa.Substring(0, 20) + "...";
                g.DrawString(nazwa, fontData, brushBlack, colX[2] + 3, textY);

                // DATA BAD. SALMONELI
                DrawCenteredText(g, d.DataSalmonella ?? "", fontData, brushGray, colX[3], colWidths[3], textY);

                // NR ŚW. ZDROWIA
                DrawCenteredText(g, d.NrSwZdrowia ?? "", fontData, brushGray, colX[4], colWidths[4], textY);

                // NR GOSPODARSTWA
                DrawCenteredText(g, d.AnimNo ?? "", fontData, brushBlack, colX[5], colWidths[5], textY);

                // ILOŚĆ
                DrawCenteredText(g, d.SztukiDek.ToString(), fontDataBold, brushBlack, colX[6], colWidths[6], textY);

                // WPR AUTO (CarID)
                string carId = d.CarID ?? "";
                if (carId.Length > 10) carId = carId.Substring(0, 10);
                DrawCenteredText(g, carId, fontData, brushGray, colX[7], colWidths[7], textY);

                // PADŁE
                string padleText = d.Padle > 0 ? d.Padle.ToString() : "-";
                DrawCenteredText(g, padleText, fontDataBold, brushBlack, colX[8], colWidths[8], textY);

                // ZDATNE I+II (ID z Dostawcy)
                DrawCenteredText(g, d.ZdatneID ?? "", fontData, brushGray, colX[9], colWidths[9], textY);

                // CH
                string chText = d.CH > 0 ? d.CH.ToString() : "-";
                DrawCenteredText(g, chText, fontData, brushBlack, colX[10], colWidths[10], textY);

                // NW
                string nwText = d.NW > 0 ? d.NW.ToString() : "-";
                DrawCenteredText(g, nwText, fontData, brushBlack, colX[11], colWidths[11], textY);

                // ZM
                string zmText = d.ZM > 0 ? d.ZM.ToString() : "-";
                DrawCenteredText(g, zmText, fontData, brushBlack, colX[12], colWidths[12], textY);

                // RAZEM
                int razem = d.CH + d.NW + d.ZM;
                string razemText = razem > 0 ? razem.ToString() : "-";
                DrawCenteredText(g, razemText, fontDataBold, brushBlack, colX[13], colWidths[13], textY);

                // Linie pionowe
                for (int i = 1; i < colWidths.Length; i++)
                {
                    g.DrawLine(penNormal, colX[i], y, colX[i], y + rowHeight);
                }

                // Sumowanie
                sumaPadle += d.Padle;
                sumaCH += d.CH;
                sumaNW += d.NW;
                sumaZM += d.ZM;
                sumaIlosc += d.SztukiDek;

                y += rowHeight;
            }

            // ═══════════════════════════════════════════════════════════════
            // WIERSZ SUMY
            // ═══════════════════════════════════════════════════════════════
            float sumRowHeight = 25;
            g.FillRectangle(brushHeaderBg, leftMargin, y, tableWidth, sumRowHeight);
            g.DrawRectangle(penThick, leftMargin, y, tableWidth, sumRowHeight);

            float sumTextY = y + 6;
            
            g.DrawString("SUMA:", fontSummary, brushBlack, colX[2] + 3, sumTextY);
            
            // Suma ILOŚĆ
            DrawCenteredText(g, sumaIlosc.ToString(), fontSummary, brushBlack, colX[6], colWidths[6], sumTextY);
            
            // Suma PADŁE
            DrawCenteredText(g, sumaPadle.ToString(), fontSummary, brushBlack, colX[8], colWidths[8], sumTextY);
            
            // Suma CH
            DrawCenteredText(g, sumaCH.ToString(), fontSummary, brushBlack, colX[10], colWidths[10], sumTextY);
            
            // Suma NW
            DrawCenteredText(g, sumaNW.ToString(), fontSummary, brushBlack, colX[11], colWidths[11], sumTextY);
            
            // Suma ZM
            DrawCenteredText(g, sumaZM.ToString(), fontSummary, brushBlack, colX[12], colWidths[12], sumTextY);
            
            // Suma RAZEM
            int sumaRazem = sumaCH + sumaNW + sumaZM;
            DrawCenteredText(g, sumaRazem.ToString(), fontSummary, brushBlack, colX[13], colWidths[13], sumTextY);

            // Linie pionowe sumy
            for (int i = 1; i < colWidths.Length; i++)
            {
                g.DrawLine(penNormal, colX[i], y, colX[i], y + sumRowHeight);
            }

            // Cleanup
            fontTitle.Dispose();
            fontSubtitle.Dispose();
            fontHeader.Dispose();
            fontData.Dispose();
            fontDataBold.Dispose();
            fontSummary.Dispose();
            brushBlack.Dispose();
            brushGray.Dispose();
            brushHeaderBg.Dispose();
            penThick.Dispose();
            penNormal.Dispose();

            e.HasMorePages = false;
        }

        private void DrawCenteredText(Graphics g, string text, System.Drawing.Font font, SolidBrush brush, float x, float width, float y)
        {
            SizeF size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, x + (width - size.Width) / 2, y);
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
        public int ID { get; set; }
        public string Lp { get; set; }
        public string CustomerGID { get; set; }
        public string HodowcaNazwa { get; set; }
        public string AnimNo { get; set; }
        public string Adres { get; set; }
        public string CarID { get; set; }
        public string TrailerID { get; set; }
        public int SztukiDek { get; set; }
        public int Netto { get; set; }
        public int LUMEL { get; set; }
        
        // Nowe pola dla płachty
        public string DataSalmonella { get; set; }  // Data badania salmonelli
        public string NrSwZdrowia { get; set; }     // Nr świadectwa zdrowia
        public string ZdatneID { get; set; }        // ID z tabeli Dostawcy (ZDATNE I+II)
        
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
        public string PojazdDisplay 
        { 
            get 
            {
                if (!string.IsNullOrEmpty(CarID) && !string.IsNullOrEmpty(TrailerID))
                    return $"{CarID}/{TrailerID}";
                if (!string.IsNullOrEmpty(CarID))
                    return CarID;
                if (!string.IsNullOrEmpty(TrailerID))
                    return TrailerID;
                return "-";
            }
        }
        
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
                    case StatusOceny.Nieoceniona: return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F44336")); // Czerwony
                    case StatusOceny.CzesciowaOcena: return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF9800")); // Pomarańczowy
                    case StatusOceny.Oceniona: return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4CAF50")); // Zielony
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
