using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Kalendarz1
{
    /// <summary>
    /// Panel Portiera - dotykowy interfejs do rejestracji wag dostaw żywca
    /// Zapisuje wagi do tabeli FarmerCalc wraz z historią (kto, kiedy, skąd)
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

        // Źródło zapisu wagi
        private const string WEIGHT_SOURCE = "PanelPortiera";

        // Timery
        private System.Windows.Threading.DispatcherTimer autoRefreshTimer;
        private System.Windows.Threading.DispatcherTimer dateCheckTimer;

        // Waga elektroniczna
        private SerialPort serialPort;
        private bool scaleConnected = false;
        private StringBuilder scaleBuffer = new StringBuilder();

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

            // Domyślnie aktywne pole Brutto
            SetAktywnePole(AktywnePole.Brutto);

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
            // Auto-odświeżanie listy dostaw co 5 minut
            LoadDostawy();

            // Jeśli była wybrana dostawa, spróbuj ją odświeżyć
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
            // Sprawdź czy wybrana data to dzisiejsza data
            if (selectedDate.Date != DateTime.Today)
            {
                // Automatycznie przełącz na dzisiejszą datę
                selectedDate = DateTime.Today;
                UpdateDateDisplay();
                LoadDostawy();
                ClearWybranaDostwa();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // Zatrzymaj timery przy zamykaniu okna
            autoRefreshTimer?.Stop();
            dateCheckTimer?.Stop();

            // Rozłącz wagę
            if (scaleConnected)
            {
                DisconnectScale();
            }

            base.OnClosed(e);
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
                    // Używamy subquery do pobrania nazwy hodowcy (obsługa varchar ID ze spacjami)
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
                            fc.FarmWeightDate,
                            fc.FarmWeightUser,
                            fc.FarmWeightSource,
                            fc.NotkaWozek
                        FROM dbo.FarmerCalc fc
                        LEFT JOIN dbo.Driver dr ON fc.DriverGID = dr.GID
                        WHERE CAST(fc.CalcDate AS DATE) = @Data
                        ORDER BY 
                            CASE WHEN ISNUMERIC(fc.LpDostawy) = 1 THEN CAST(fc.LpDostawy AS INT) ELSE 999999 END,
                            fc.ID";

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

                                // Historia wagi
                                if (reader["FarmWeightDate"] != DBNull.Value)
                                    dostawa.WagaData = Convert.ToDateTime(reader["FarmWeightDate"]);
                                dostawa.WagaUser = reader["FarmWeightUser"]?.ToString() ?? "";
                                dostawa.WagaSource = reader["FarmWeightSource"]?.ToString() ?? "";

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

        #endregion

        #region Obsługa UI

        private void UpdateDateDisplay()
        {
            lblData.Text = selectedDate.ToString("dd.MM.yyyy");
            var culture = new CultureInfo("pl-PL");
            string dayName = selectedDate.ToString("dddd", culture);
            lblDzienTygodnia.Text = char.ToUpper(dayName[0]) + dayName.Substring(1);
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

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadDostawy();

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

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DostawaPortiera dostawa)
            {
                int index = dostawy.IndexOf(dostawa);
                if (index > 0)
                {
                    dostawy.Move(index, index - 1);
                    UpdateLpNumbers();
                    SaveOrderToDatabase();
                }
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DostawaPortiera dostawa)
            {
                int index = dostawy.IndexOf(dostawa);
                if (index < dostawy.Count - 1)
                {
                    dostawy.Move(index, index + 1);
                    UpdateLpNumbers();
                    SaveOrderToDatabase();
                }
            }
        }

        private void UpdateLpNumbers()
        {
            for (int i = 0; i < dostawy.Count; i++)
            {
                dostawy[i].Lp = (i + 1).ToString();
                dostawy[i].LpDostawy = (i + 1).ToString();
            }
            listDostawy.Items.Refresh();
        }

        private void SaveOrderToDatabase()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            for (int i = 0; i < dostawy.Count; i++)
                            {
                                string updateQuery = "UPDATE dbo.FarmerCalc SET LpDostawy = @Lp WHERE ID = @ID";
                                using (SqlCommand cmd = new SqlCommand(updateQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@Lp", i + 1);
                                    cmd.Parameters.AddWithValue("@ID", dostawy[i].ID);
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu kolejności:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
            lblWybranyKierowca.Text = $"🚚 {dostawa.KierowcaNazwa} • {dostawa.CarID} / {dostawa.TrailerID}";

            // Status i historia
            if (!string.IsNullOrEmpty(dostawa.WagaUser))
            {
                lblStatus.Text = $"{dostawa.WagaSource}";
            }
            else
            {
                lblStatus.Text = "";
            }

            // Timestamp zapisu wagi
            if (dostawa.WagaData.HasValue)
            {
                lblWagaTimestamp.Text = $"✓ Zapisano: {dostawa.WagaData.Value:dd.MM.yyyy HH:mm:ss} przez {dostawa.WagaUser}";
            }
            else
            {
                lblWagaTimestamp.Text = "";
            }

            // Wczytaj zapisane wagi
            txtBrutto.Text = dostawa.Brutto > 0 ? dostawa.Brutto.ToString() : "0";
            txtTara.Text = dostawa.Tara > 0 ? dostawa.Tara.ToString() : "0";
            UpdateNetto();

            // Włącz przycisk zapisu
            btnZapisz.IsEnabled = true;

            // Włącz przycisk drukowania jeśli są wagi
            btnDrukuj.IsEnabled = dostawa.Netto > 0;

            // Ustaw aktywne pole na Brutto
            SetAktywnePole(AktywnePole.Brutto);
        }

        private void ClearWybranaDostwa()
        {
            wybranaDostwa = null;
            lblWybranyLp.Text = "LP: -";
            lblWybranyHodowca.Text = "Wybierz dostawę z listy";
            lblWybranyKierowca.Text = "";
            lblStatus.Text = "";
            lblWagaTimestamp.Text = "";
            txtBrutto.Text = "0";
            txtTara.Text = "0";
            txtNetto.Text = "0 kg";
            btnZapisz.IsEnabled = false;
            btnDrukuj.IsEnabled = false;
        }

        #endregion

        #region Klawiatura numeryczna

        private void SetAktywnePole(AktywnePole pole)
        {
            aktywnePole = pole;

            // Podświetl aktywne pole
            if (pole == AktywnePole.Brutto)
            {
                borderBrutto.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA726"));
                borderTara.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F3460"));
            }
            else
            {
                borderBrutto.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F3460"));
                borderTara.BorderBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#42A5F5"));
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

            // Pobierz nazwę użytkownika
            string userName = App.UserFullName ?? App.UserID ?? "Nieznany";

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Zapisz wagi od hodowcy do FarmerCalc wraz z historią
                    string updateQuery = @"
                        UPDATE dbo.FarmerCalc 
                        SET FullFarmWeight = @Brutto,
                            EmptyFarmWeight = @Tara,
                            NettoFarmWeight = @Netto,
                            FarmWeightDate = @WeightDate,
                            FarmWeightUser = @WeightUser,
                            FarmWeightSource = @WeightSource
                        WHERE ID = @ID";

                    using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@Brutto", brutto);
                        cmd.Parameters.AddWithValue("@Tara", tara);
                        cmd.Parameters.AddWithValue("@Netto", netto);
                        cmd.Parameters.AddWithValue("@WeightDate", DateTime.Now);
                        cmd.Parameters.AddWithValue("@WeightUser", userName);
                        cmd.Parameters.AddWithValue("@WeightSource", WEIGHT_SOURCE);
                        cmd.Parameters.AddWithValue("@ID", wybranaDostwa.ID);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            // Aktualizuj lokalnie
                            DateTime savedTime = DateTime.Now;
                            wybranaDostwa.Brutto = brutto;
                            wybranaDostwa.Tara = tara;
                            wybranaDostwa.Netto = netto;
                            wybranaDostwa.WagaData = savedTime;
                            wybranaDostwa.WagaUser = userName;
                            wybranaDostwa.WagaSource = WEIGHT_SOURCE;
                            wybranaDostwa.UpdateStatus();

                            // Aktualizuj timestamp w UI
                            lblWagaTimestamp.Text = $"✓ Zapisano: {savedTime:dd.MM.yyyy HH:mm:ss} przez {userName}";

                            // Włącz przycisk drukowania
                            btnDrukuj.IsEnabled = true;

                            // Odśwież listę
                            listDostawy.Items.Refresh();

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

        #endregion

        #region Drukowanie kwitu wagowego

        private DostawaPortiera dostawaDoDruku;

        private void BtnDrukuj_Click(object sender, RoutedEventArgs e)
        {
            if (wybranaDostwa == null || wybranaDostwa.Netto <= 0)
            {
                MessageBox.Show("Wybierz dostawę z zapisaną wagą!", "Uwaga",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            dostawaDoDruku = wybranaDostwa;

            try
            {
                PrintDocument printDoc = new PrintDocument();
                printDoc.PrintPage += PrintDoc_PrintPage;

                // Pokaż dialog drukowania
                System.Windows.Forms.PrintDialog printDialog = new System.Windows.Forms.PrintDialog();
                printDialog.Document = printDoc;

                if (printDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    printDoc.Print();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd drukowania:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintDoc_PrintPage(object sender, PrintPageEventArgs e)
        {
            Graphics g = e.Graphics;

            // Fonty dla drukarki termicznej 58mm (szerokość ~48mm do druku)
            // Około 32 znaków na linię dla fontu 9pt
            System.Drawing.Font fontTytul = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold);
            System.Drawing.Font fontNaglowek = new System.Drawing.Font("Arial", 9, System.Drawing.FontStyle.Bold);
            System.Drawing.Font fontNormal = new System.Drawing.Font("Arial", 8);
            System.Drawing.Font fontDuzy = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold);
            System.Drawing.Font fontMaly = new System.Drawing.Font("Arial", 7);

            System.Drawing.Brush brushCzarny = System.Drawing.Brushes.Black;
            System.Drawing.Pen penCzarny = new System.Drawing.Pen(System.Drawing.Color.Black, 1);

            float y = 5;
            float leftMargin = 5;
            float width = 180; // ~48mm w pikselach (58mm - marginesy)
            float centerX = leftMargin + width / 2;

            // Funkcja do centrowania tekstu
            StringFormat centerFormat = new StringFormat { Alignment = StringAlignment.Center };
            StringFormat leftFormat = new StringFormat { Alignment = StringAlignment.Near };
            StringFormat rightFormat = new StringFormat { Alignment = StringAlignment.Far };

            // === NAGŁÓWEK ===
            g.DrawString("UBOJNIA DROBIU", fontTytul, brushCzarny, centerX, y, centerFormat);
            y += 16;
            g.DrawString("PIÓRKOWSCY", fontTytul, brushCzarny, centerX, y, centerFormat);
            y += 16;
            g.DrawString("Brzeziny k. Łodzi", fontMaly, brushCzarny, centerX, y, centerFormat);
            y += 14;

            // Linia
            g.DrawLine(penCzarny, leftMargin, y, leftMargin + width, y);
            y += 5;

            // === KWIT WAGOWY ===
            g.DrawString("KWIT WAGOWY", fontTytul, brushCzarny, centerX, y, centerFormat);
            y += 18;

            // Linia
            g.DrawLine(penCzarny, leftMargin, y, leftMargin + width, y);
            y += 8;

            // === DATA I LP ===
            g.DrawString($"Data: {selectedDate:dd.MM.yyyy}", fontNormal, brushCzarny, leftMargin, y);
            g.DrawString($"LP: {dostawaDoDruku.Lp}", fontNormal, brushCzarny, leftMargin + width, y, rightFormat);
            y += 14;

            // === HODOWCA ===
            g.DrawString("HODOWCA:", fontNaglowek, brushCzarny, leftMargin, y);
            y += 12;

            // Podziel długą nazwę hodowcy na linie
            string hodowca = dostawaDoDruku.HodowcaNazwa ?? "-";
            if (hodowca.Length > 24)
            {
                g.DrawString(hodowca.Substring(0, 24), fontNormal, brushCzarny, leftMargin, y);
                y += 11;
                g.DrawString(hodowca.Substring(24), fontNormal, brushCzarny, leftMargin, y);
            }
            else
            {
                g.DrawString(hodowca, fontNormal, brushCzarny, leftMargin, y);
            }
            y += 14;

            // === KIEROWCA ===
            g.DrawString("KIEROWCA:", fontNaglowek, brushCzarny, leftMargin, y);
            y += 12;
            g.DrawString(dostawaDoDruku.KierowcaNazwa ?? "-", fontNormal, brushCzarny, leftMargin, y);
            y += 14;

            // === POJAZD ===
            g.DrawString($"Auto: {dostawaDoDruku.CarID ?? "-"}", fontNormal, brushCzarny, leftMargin, y);
            y += 11;
            g.DrawString($"Nacz: {dostawaDoDruku.TrailerID ?? "-"}", fontNormal, brushCzarny, leftMargin, y);
            y += 14;

            // Linia
            g.DrawLine(penCzarny, leftMargin, y, leftMargin + width, y);
            y += 8;

            // === WAGI ===
            g.DrawString("WYNIKI WAŻENIA", fontNaglowek, brushCzarny, centerX, y, centerFormat);
            y += 16;

            // Brutto
            g.DrawString("BRUTTO:", fontNormal, brushCzarny, leftMargin, y);
            g.DrawString($"{dostawaDoDruku.Brutto:N0} kg", fontDuzy, brushCzarny, leftMargin + width, y, rightFormat);
            y += 18;

            // Tara
            g.DrawString("TARA:", fontNormal, brushCzarny, leftMargin, y);
            g.DrawString($"{dostawaDoDruku.Tara:N0} kg", fontDuzy, brushCzarny, leftMargin + width, y, rightFormat);
            y += 18;

            // Linia przed netto
            g.DrawLine(penCzarny, leftMargin + 80, y, leftMargin + width, y);
            y += 5;

            // Netto - wyróżnione
            g.DrawString("NETTO:", fontNaglowek, brushCzarny, leftMargin, y);
            g.DrawString($"{dostawaDoDruku.Netto:N0} kg", fontDuzy, brushCzarny, leftMargin + width, y, rightFormat);
            y += 20;

            // Linia
            g.DrawLine(penCzarny, leftMargin, y, leftMargin + width, y);
            y += 8;

            // === SZTUKI ===
            if (dostawaDoDruku.SztukiPlan > 0)
            {
                g.DrawString($"Szt. plan.: {dostawaDoDruku.SztukiPlan:N0}", fontMaly, brushCzarny, leftMargin, y);
                y += 12;
            }

            // === TIMESTAMP ===
            string timestamp = dostawaDoDruku.WagaData.HasValue
                ? dostawaDoDruku.WagaData.Value.ToString("dd.MM.yyyy HH:mm:ss")
                : DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

            g.DrawString($"Ważono: {timestamp}", fontMaly, brushCzarny, leftMargin, y);
            y += 10;

            string user = !string.IsNullOrEmpty(dostawaDoDruku.WagaUser)
                ? dostawaDoDruku.WagaUser
                : (App.UserFullName ?? App.UserID ?? "-");
            g.DrawString($"Zważył: {user}", fontMaly, brushCzarny, leftMargin, y);
            y += 16;

            // Linia
            g.DrawLine(penCzarny, leftMargin, y, leftMargin + width, y);
            y += 12;

            // === PODPISY ===
            g.DrawString("Podpis portiera:", fontMaly, brushCzarny, leftMargin, y);
            y += 20;
            g.DrawLine(penCzarny, leftMargin, y, leftMargin + 80, y);
            y += 12;

            g.DrawString("Podpis kierowcy:", fontMaly, brushCzarny, leftMargin, y);
            y += 20;
            g.DrawLine(penCzarny, leftMargin, y, leftMargin + 80, y);
            y += 15;

            // === STOPKA ===
            g.DrawLine(penCzarny, leftMargin, y, leftMargin + width, y);
            y += 5;
            g.DrawString($"Wydruk: {DateTime.Now:HH:mm:ss}", fontMaly, brushCzarny, centerX, y, centerFormat);

            e.HasMorePages = false;
        }

        #endregion

        #region Integracja z wagą elektroniczną

        private void BtnConnectScale_Click(object sender, RoutedEventArgs e)
        {
            if (scaleConnected)
            {
                DisconnectScale();
            }
            else
            {
                ShowScaleConnectionDialog();
            }
        }

        private void ShowScaleConnectionDialog()
        {
            // Pobierz dostępne porty COM
            string[] ports = SerialPort.GetPortNames();

            if (ports.Length == 0)
            {
                MessageBox.Show("Nie znaleziono żadnych portów COM.\nSprawdź czy waga jest podłączona.",
                    "Brak portów", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Prosty dialog wyboru portu
            var dialog = new Window
            {
                Title = "Połącz z wagą",
                Width = 350,
                Height = 280,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A1A2E")),
                ResizeMode = ResizeMode.NoResize
            };

            var stack = new StackPanel { Margin = new Thickness(20) };

            // Wybór portu
            stack.Children.Add(new TextBlock
            {
                Text = "Port COM:",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 5)
            });

            var cmbPort = new ComboBox { Height = 30, FontSize = 14 };
            foreach (var port in ports) cmbPort.Items.Add(port);
            cmbPort.SelectedIndex = 0;
            stack.Children.Add(cmbPort);

            // Prędkość transmisji
            stack.Children.Add(new TextBlock
            {
                Text = "Prędkość (baud):",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                Margin = new Thickness(0, 15, 0, 5)
            });

            var cmbBaud = new ComboBox { Height = 30, FontSize = 14 };
            int[] baudRates = { 9600, 19200, 38400, 57600, 115200 };
            foreach (var baud in baudRates) cmbBaud.Items.Add(baud);
            cmbBaud.SelectedItem = 9600;
            stack.Children.Add(cmbBaud);

            // Przyciski
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var btnConnect = new Button
            {
                Content = "Połącz",
                Width = 100,
                Height = 35,
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00BFA6")),
                Foreground = System.Windows.Media.Brushes.White,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var btnCancel = new Button
            {
                Content = "Anuluj",
                Width = 80,
                Height = 35,
                Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E94560")),
                Foreground = System.Windows.Media.Brushes.White
            };

            btnConnect.Click += (s, args) =>
            {
                string selectedPort = cmbPort.SelectedItem?.ToString();
                int selectedBaud = (int)cmbBaud.SelectedItem;

                if (ConnectToScale(selectedPort, selectedBaud))
                {
                    dialog.DialogResult = true;
                    dialog.Close();
                }
            };

            btnCancel.Click += (s, args) => dialog.Close();

            btnPanel.Children.Add(btnConnect);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            dialog.Content = stack;
            dialog.ShowDialog();
        }

        private bool ConnectToScale(string portName, int baudRate)
        {
            try
            {
                serialPort = new SerialPort(portName)
                {
                    BaudRate = baudRate,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    Encoding = Encoding.ASCII
                };

                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.Open();

                scaleConnected = true;
                UpdateScaleStatus();

                MessageBox.Show($"Połączono z wagą na porcie {portName}", "Sukces",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd połączenia z wagą:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void DisconnectScale()
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.DataReceived -= SerialPort_DataReceived;
                    serialPort.Close();
                    serialPort.Dispose();
                    serialPort = null;
                }

                scaleConnected = false;
                UpdateScaleStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd rozłączania wagi:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateScaleStatus()
        {
            if (scaleConnected && serialPort != null)
            {
                lblScaleStatus.Text = $"⚖️ Waga: Połączona ({serialPort.PortName})";
                lblScaleStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#66BB6A"));
                btnConnectScale.Content = "Rozłącz";
                btnConnectScale.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E94560"));
            }
            else
            {
                lblScaleStatus.Text = "⚖️ Waga: Niepodłączona";
                lblScaleStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8"));
                btnConnectScale.Content = "Połącz";
                btnConnectScale.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#16213E"));
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = serialPort.ReadExisting();
                scaleBuffer.Append(data);

                // Sprawdź czy mamy kompletną linię (zakończoną CR lub LF)
                string bufferContent = scaleBuffer.ToString();
                if (bufferContent.Contains("\r") || bufferContent.Contains("\n"))
                {
                    string[] lines = bufferContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    if (lines.Length > 0)
                    {
                        string lastLine = lines[lines.Length - 1];
                        int weight = ParseWeightFromScale(lastLine);

                        if (weight > 0)
                        {
                            // Aktualizuj UI w wątku głównym
                            Dispatcher.Invoke(() =>
                            {
                                if (aktywnePole == AktywnePole.Brutto)
                                {
                                    txtBrutto.Text = weight.ToString();
                                }
                                else
                                {
                                    txtTara.Text = weight.ToString();
                                }
                                UpdateNetto();
                            });
                        }
                    }

                    scaleBuffer.Clear();
                }
            }
            catch (Exception)
            {
                // Ignoruj błędy odczytu
            }
        }

        private int ParseWeightFromScale(string data)
        {
            // RHEWA 82c-1 format danych
            // Typowe formaty RHEWA:
            // "G     12345 kg" - waga brutto
            // "N     12345 kg" - waga netto
            // "T     12345 kg" - tara
            // Lub sam numer: "     12345"

            try
            {
                // Usuń prefiksy RHEWA (G, N, T, ST, US, OL)
                string cleanData = data.Trim();

                // Usuń znane prefiksy
                string[] prefixes = { "G", "N", "T", "ST", "US", "OL", "GS", "NS" };
                foreach (var prefix in prefixes)
                {
                    if (cleanData.StartsWith(prefix))
                    {
                        cleanData = cleanData.Substring(prefix.Length);
                        break;
                    }
                }

                // Usuń jednostkę kg
                cleanData = cleanData.Replace("kg", "").Replace("KG", "").Replace("Kg", "");

                // Usuń wszystko oprócz cyfr, minus i kropki/przecinka
                cleanData = cleanData.Trim();

                // Obsłuż przecinek jako separator dziesiętny
                cleanData = cleanData.Replace(",", ".");

                // Wyciągnij tylko cyfry i minus
                string numericPart = new string(cleanData.Where(c => char.IsDigit(c) || c == '-' || c == '.').ToArray());

                // Parsuj jako decimal i zaokrąglij do int
                if (decimal.TryParse(numericPart, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal weight))
                {
                    return (int)Math.Abs(Math.Round(weight));
                }

                // Alternatywnie - tylko cyfry
                string digitsOnly = new string(data.Where(char.IsDigit).ToArray());
                if (int.TryParse(digitsOnly, out int intWeight))
                {
                    return Math.Abs(intWeight);
                }
            }
            catch
            {
                // Ignoruj błędy parsowania
            }

            return 0;
        }

        private void BtnReadScale_Click(object sender, RoutedEventArgs e)
        {
            if (!scaleConnected || serialPort == null || !serialPort.IsOpen)
            {
                MessageBox.Show("Waga nie jest podłączona!\nKliknij 'Połącz' aby połączyć z wagą.",
                    "Brak połączenia", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Określ które pole aktualizować
            if (sender is Button btn && btn.Tag != null)
            {
                if (btn.Tag.ToString() == "Brutto")
                {
                    SetAktywnePole(AktywnePole.Brutto);
                }
                else if (btn.Tag.ToString() == "Tara")
                {
                    SetAktywnePole(AktywnePole.Tara);
                }
            }

            try
            {
                // RHEWA 82c - komendy do odczytu wagi:
                // "W" - odczyt wagi (weight)
                // "G" - odczyt brutto (gross)
                // "N" - odczyt netto (net)
                // "S" - odczyt stabilny
                // Lub ESC + "p" dla niektórych modeli

                // Wyczyść bufor przed odczytem
                scaleBuffer.Clear();
                serialPort.DiscardInBuffer();

                // Wyślij komendę - RHEWA zazwyczaj używa "S" lub CR
                serialPort.WriteLine("S");

                // Daj chwilę na odpowiedź
                System.Threading.Thread.Sleep(200);

                // Spróbuj odczytać bezpośrednio jeśli są dane
                if (serialPort.BytesToRead > 0)
                {
                    string response = serialPort.ReadExisting();
                    int weight = ParseWeightFromScale(response);

                    if (weight > 0)
                    {
                        if (aktywnePole == AktywnePole.Brutto)
                        {
                            txtBrutto.Text = weight.ToString();
                        }
                        else
                        {
                            txtTara.Text = weight.ToString();
                        }
                        UpdateNetto();
                    }
                    else
                    {
                        MessageBox.Show("Nie udało się odczytać wagi.\nSprawdź czy waga jest stabilna.",
                            "Błąd odczytu", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd odczytu z wagi:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        // Historia wagi
        public DateTime? WagaData { get; set; }
        public string WagaUser { get; set; }
        public string WagaSource { get; set; }

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
        public string SztukiPlanDisplay => SztukiPlan > 0 ? $"🐔 {SztukiPlan:N0} szt" : "";

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
                    case StatusDostawy.Oczekuje: return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA726"));
                    case StatusDostawy.WTrakcie: return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#42A5F5"));
                    case StatusDostawy.Zakonczony: return new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#66BB6A"));
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