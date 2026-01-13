using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Kalendarz1.Zywiec.WidokSpecyfikacji
{
    public partial class NowaSpecyfikacjaWindow : Window
    {
        #region Pola

        private string connectionString;

        public bool SpecyfikacjaCreated { get; private set; } = false;
        public int CreatedSpecId { get; private set; } = 0;
        public bool PrintPdf { get; set; } = false;

        // Ustawienia do skopiowania po utworzeniu (z duplikatu)
        public DuplikatUstawienia UstawieniaDoDuplikacji { get; private set; } = null;

        private List<HodowcaItem> hodowcyList = new List<HodowcaItem>();
        private List<string> ciagnikiList = new List<string>();
        private List<string> naczepyList = new List<string>();
        private List<KierowcaItem> kierowcyList = new List<KierowcaItem>();
        private List<IstniejacaSpecItem> istniejaceSpecList = new List<IstniejacaSpecItem>();

        // Zapamiętane ostatnie wybory
        private static string _lastCiagnik = "";
        private static string _lastNaczepa = "";
        private static int? _lastKierowcaGID = null;

        // Pre-fill hodowca (z duplikatu)
        private string _prefillHodowcaId = null;
        private DuplikatUstawienia _sourceUstawienia = null;

        #endregion

        #region Konstruktory

        /// <summary>
        /// Standardowy konstruktor
        /// </summary>
        public NowaSpecyfikacjaWindow(string connString, DateTime? defaultDate = null)
            : this(connString, defaultDate, null, null)
        {
        }

        /// <summary>
        /// Konstruktor z możliwością duplikacji ustawień
        /// </summary>
        /// <param name="connString">Connection string</param>
        /// <param name="defaultDate">Data uboju</param>
        /// <param name="prefillHodowcaId">ID hodowcy do wypełnienia (CustomerGID)</param>
        /// <param name="ustawieniaZrodlowe">Ustawienia cenowe do skopiowania po zapisie</param>
        public NowaSpecyfikacjaWindow(string connString, DateTime? defaultDate, 
            string prefillHodowcaId, DuplikatUstawienia ustawieniaZrodlowe)
        {
            InitializeComponent();
            connectionString = connString;
            _prefillHodowcaId = prefillHodowcaId;
            _sourceUstawienia = ustawieniaZrodlowe;

            DateTime dataUboju = defaultDate ?? DateTime.Today;

            this.Loaded += (s, e) =>
            {
                dpDataUboju.SelectedDate = dataUboju;
                LoadHodowcy();
                LoadCiagniki();
                LoadNaczepy();
                LoadKierowcy();
                LoadIstniejaceSpec(dataUboju);
                UpdateNextNumber();
                RestoreLastSelections();
                
                // Jeśli to duplikacja - wypełnij hodowcę i pokaż info
                if (!string.IsNullOrEmpty(_prefillHodowcaId))
                {
                    SetupDuplikat();
                }
                
                cmbHodowca.Focus();
            };
        }

        #endregion

        #region Duplikacja

        private void SetupDuplikat()
        {
            // Znajdź i zaznacz hodowcę
            var hodowca = hodowcyList.FirstOrDefault(h => h.ID == _prefillHodowcaId);
            if (hodowca != null)
            {
                cmbHodowca.SelectedItem = hodowca;
            }

            // Pokaż panel info
            if (_sourceUstawienia != null)
            {
                pnlDuplikatInfo.Visibility = Visibility.Visible;
                lblTytul.Text = "📋 Duplikuj Specyfikację";
                
                string info = $"Hodowca: {_sourceUstawienia.DostawcaNazwa}\n";
                info += $"Cena: {_sourceUstawienia.Cena:F2} zł, Typ: {_sourceUstawienia.TypCeny}\n";
                info += $"Ubytek: {_sourceUstawienia.Ubytek}%, PIK: {_sourceUstawienia.PIK}";
                
                lblDuplikatInfo.Text = info;
                lblInfo.Text = "Ustawienia cenowe zostaną skopiowane po zapisie";

                // Zapisz ustawienia do skopiowania
                UstawieniaDoDuplikacji = _sourceUstawienia;
            }
        }

        #endregion

        #region Ładowanie istniejących specyfikacji do powiązania

        private void LoadIstniejaceSpec(DateTime date)
        {
            istniejaceSpecList.Clear();
            
            istniejaceSpecList.Add(new IstniejacaSpecItem 
            { 
                ID = 0, 
                CarLp = 0, 
                Hodowca = "(bez powiązania - nowa dostawa)", 
                Ciagnik = "",
                Naczepa = "",
                KierowcaGID = null
            });

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = @"
                        SELECT fc.ID, fc.CarLp, fc.CarID, fc.TrailerID, fc.DriverGID,
                               ISNULL(d.ShortName, fc.CustomerGID) AS Hodowca
                        FROM dbo.FarmerCalc fc
                        LEFT JOIN dbo.Dostawcy d ON fc.CustomerGID = d.ID
                        WHERE fc.CalcDate = @Date
                        ORDER BY fc.CarLp";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Date", date.Date);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                istniejaceSpecList.Add(new IstniejacaSpecItem
                                {
                                    ID = Convert.ToInt32(reader["ID"]),
                                    CarLp = Convert.ToInt32(reader["CarLp"]),
                                    Hodowca = reader["Hodowca"]?.ToString() ?? "",
                                    Ciagnik = reader["CarID"]?.ToString() ?? "",
                                    Naczepa = reader["TrailerID"]?.ToString() ?? "",
                                    KierowcaGID = reader["DriverGID"] != DBNull.Value ? Convert.ToInt32(reader["DriverGID"]) : (int?)null
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading istniejace spec: {ex.Message}");
            }

            cmbPowiazanaSpec.ItemsSource = istniejaceSpecList;
            cmbPowiazanaSpec.SelectedIndex = 0;
        }

        private void CmbPowiazanaSpec_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbPowiazanaSpec.SelectedItem is IstniejacaSpecItem selected && selected.ID > 0)
            {
                cmbCiagnik.Text = selected.Ciagnik;
                cmbNaczepa.Text = selected.Naczepa;
                
                if (selected.KierowcaGID.HasValue)
                {
                    var kierowca = kierowcyList.FirstOrDefault(k => k.GID == selected.KierowcaGID.Value);
                    if (kierowca != null)
                        cmbKierowca.SelectedItem = kierowca;
                }

                lblOstatni.Text = $"🔗 Powiązano z dostawą nr {selected.CarLp}";
            }
        }

        #endregion

        #region Skróty klawiaturowe

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !IsComboBoxDropDownOpen())
            {
                BtnZapisz_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                BtnAnuluj_Click(sender, e);
                e.Handled = true;
            }
        }

        private bool IsComboBoxDropDownOpen()
        {
            return (cmbHodowca?.IsDropDownOpen == true) ||
                   (cmbCiagnik?.IsDropDownOpen == true) ||
                   (cmbNaczepa?.IsDropDownOpen == true) ||
                   (cmbKierowca?.IsDropDownOpen == true) ||
                   (cmbPowiazanaSpec?.IsDropDownOpen == true);
        }

        #endregion

        #region Ładowanie danych

        private void LoadHodowcy()
        {
            hodowcyList.Clear();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT ID, ShortName FROM dbo.Dostawcy WHERE halt = 0 ORDER BY ShortName";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            hodowcyList.Add(new HodowcaItem
                            {
                                ID = reader["ID"]?.ToString()?.Trim() ?? "",
                                Nazwa = reader["ShortName"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading hodowcy: {ex.Message}");
            }

            if (cmbHodowca != null)
            {
                cmbHodowca.ItemsSource = hodowcyList;
                cmbHodowca.DisplayMemberPath = "DisplayName";
                cmbHodowca.SelectedValuePath = "ID";
            }
        }

        private void LoadCiagniki()
        {
            ciagnikiList.Clear();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT DISTINCT ID FROM dbo.CarTrailer WHERE kind = '1' ORDER BY ID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            ciagnikiList.Add(reader["ID"]?.ToString() ?? "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading ciagniki: {ex.Message}");
            }

            if (!string.IsNullOrEmpty(_lastCiagnik) && ciagnikiList.Contains(_lastCiagnik))
            {
                ciagnikiList.Remove(_lastCiagnik);
                ciagnikiList.Insert(0, _lastCiagnik);
            }

            if (cmbCiagnik != null)
            {
                cmbCiagnik.ItemsSource = ciagnikiList;
            }
        }

        private void LoadNaczepy()
        {
            naczepyList.Clear();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT DISTINCT ID FROM dbo.CarTrailer WHERE kind = '2' ORDER BY ID";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            naczepyList.Add(reader["ID"]?.ToString() ?? "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading naczepy: {ex.Message}");
            }

            if (!string.IsNullOrEmpty(_lastNaczepa) && naczepyList.Contains(_lastNaczepa))
            {
                naczepyList.Remove(_lastNaczepa);
                naczepyList.Insert(0, _lastNaczepa);
            }

            if (cmbNaczepa != null)
            {
                cmbNaczepa.ItemsSource = naczepyList;
            }
        }

        private void LoadKierowcy()
        {
            kierowcyList.Clear();

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT GID, Name FROM dbo.Driver WHERE halt = 0 ORDER BY Name";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            kierowcyList.Add(new KierowcaItem
                            {
                                GID = Convert.ToInt32(reader["GID"]),
                                Nazwa = reader["Name"]?.ToString() ?? ""
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading kierowcy: {ex.Message}");
            }

            if (_lastKierowcaGID.HasValue)
            {
                var last = kierowcyList.FirstOrDefault(k => k.GID == _lastKierowcaGID.Value);
                if (last != null)
                {
                    kierowcyList.Remove(last);
                    kierowcyList.Insert(0, last);
                }
            }

            if (cmbKierowca != null)
            {
                cmbKierowca.ItemsSource = kierowcyList;
                cmbKierowca.DisplayMemberPath = "Nazwa";
                cmbKierowca.SelectedValuePath = "GID";
            }
        }

        private void RestoreLastSelections()
        {
            // Nie przywracaj jeśli to duplikat
            if (!string.IsNullOrEmpty(_prefillHodowcaId))
                return;

            if (!string.IsNullOrEmpty(_lastCiagnik))
                cmbCiagnik.Text = _lastCiagnik;

            if (!string.IsNullOrEmpty(_lastNaczepa))
                cmbNaczepa.Text = _lastNaczepa;

            if (_lastKierowcaGID.HasValue)
            {
                var kierowca = kierowcyList.FirstOrDefault(k => k.GID == _lastKierowcaGID.Value);
                if (kierowca != null)
                    cmbKierowca.SelectedItem = kierowca;
            }

            if (!string.IsNullOrEmpty(_lastCiagnik))
            {
                lblOstatni.Text = $"Ostatnio: {_lastCiagnik}";
            }
        }

        private void SaveLastSelections()
        {
            _lastCiagnik = cmbCiagnik.Text ?? "";
            _lastNaczepa = cmbNaczepa.Text ?? "";

            if (cmbKierowca.SelectedItem is KierowcaItem k)
                _lastKierowcaGID = k.GID;
        }

        private void UpdateNextNumber()
        {
            if (dpDataUboju == null || lblNumerSpec == null) return;

            DateTime? date = dpDataUboju.SelectedDate;
            if (date == null) return;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT ISNULL(MAX(CarLp), 0) + 1 FROM dbo.FarmerCalc WHERE CalcDate = @Date";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Date", date.Value.Date);
                        var result = cmd.ExecuteScalar();
                        int nextNr = result != DBNull.Value ? Convert.ToInt32(result) : 1;
                        lblNumerSpec.Text = nextNr.ToString();
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Event handlers

        private void DpDataUboju_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpDataUboju.SelectedDate.HasValue)
            {
                LoadIstniejaceSpec(dpDataUboju.SelectedDate.Value);
                UpdateNextNumber();
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (dpDataUboju.SelectedDate == null)
            {
                MessageBox.Show("Wybierz datę uboju!", "Walidacja", MessageBoxButton.OK, MessageBoxImage.Warning);
                dpDataUboju.Focus();
                return;
            }

            try
            {
                DateTime date = dpDataUboju.SelectedDate.Value;
                int carLp = int.TryParse(lblNumerSpec.Text, out int nr) ? nr : 1;

                // Hodowca
                string customerGid = "";
                if (cmbHodowca.SelectedItem is HodowcaItem hodowca)
                    customerGid = hodowca.ID;
                else if (!string.IsNullOrWhiteSpace(cmbHodowca.Text))
                {
                    var found = hodowcyList.FirstOrDefault(h => 
                        h.Nazwa.Equals(cmbHodowca.Text, StringComparison.OrdinalIgnoreCase) ||
                        h.DisplayName.Equals(cmbHodowca.Text, StringComparison.OrdinalIgnoreCase));
                    if (found != null) customerGid = found.ID;
                }

                // Transport
                string carId = cmbCiagnik.Text?.Trim() ?? "";
                string trailerId = cmbNaczepa.Text?.Trim() ?? "";

                // Kierowca
                int? driverGid = null;
                if (cmbKierowca.SelectedItem is KierowcaItem kierowca)
                    driverGid = kierowca.GID;

                // Zapisz
                CreatedSpecId = SaveToDatabase(date, carLp, customerGid, carId, trailerId, driverGid);

                if (CreatedSpecId > 0)
                {
                    SpecyfikacjaCreated = true;
                    SaveLastSelections();

                    if (chkDodajKolejna.IsChecked == true)
                    {
                        cmbHodowca.SelectedItem = null;
                        cmbHodowca.Text = "";
                        
                        LoadIstniejaceSpec(date);
                        UpdateNextNumber();
                        
                        // Ukryj panel duplikatu dla kolejnych
                        pnlDuplikatInfo.Visibility = Visibility.Collapsed;
                        UstawieniaDoDuplikacji = null;
                        
                        lblOstatni.Text = $"✓ Zapisano nr {carLp}";
                        cmbHodowca.Focus();
                    }
                    else
                    {
                        this.DialogResult = true;
                        this.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int SaveToDatabase(DateTime date, int carLp, string customerGid, string carId,
                                   string trailerId, int? driverGid)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz następne ID
                    int newId = 1;
                    using (SqlCommand cmdMaxId = new SqlCommand("SELECT ISNULL(MAX(ID), 0) + 1 FROM dbo.FarmerCalc", conn))
                    {
                        var maxResult = cmdMaxId.ExecuteScalar();
                        if (maxResult != null && maxResult != DBNull.Value)
                            newId = Convert.ToInt32(maxResult);
                    }

                    // Pobierz CustomerRealGID z duplikatu jeśli jest
                    string customerRealGid = UstawieniaDoDuplikacji?.CustomerRealGID ?? "";

                    string query = @"
                        INSERT INTO dbo.FarmerCalc
                        (ID, CalcDate, CarLp, CustomerGID, CustomerRealGID, CarID, TrailerID, DriverGID,
                         DeclI1, NettoWeight)
                        VALUES
                        (@ID, @CalcDate, @CarLp, @CustomerGID, @CustomerRealGID, @CarID, @TrailerID, @DriverGID,
                         0, 0)";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", newId);
                        cmd.Parameters.AddWithValue("@CalcDate", date.Date);
                        cmd.Parameters.AddWithValue("@CarLp", carLp);
                        cmd.Parameters.AddWithValue("@CustomerGID", string.IsNullOrEmpty(customerGid) ? (object)DBNull.Value : customerGid);
                        cmd.Parameters.AddWithValue("@CustomerRealGID", string.IsNullOrEmpty(customerRealGid) ? (object)DBNull.Value : customerRealGid);
                        cmd.Parameters.AddWithValue("@CarID", string.IsNullOrEmpty(carId) ? (object)DBNull.Value : carId);
                        cmd.Parameters.AddWithValue("@TrailerID", string.IsNullOrEmpty(trailerId) ? (object)DBNull.Value : trailerId);
                        cmd.Parameters.AddWithValue("@DriverGID", driverGid.HasValue ? (object)driverGid.Value : DBNull.Value);

                        int rows = cmd.ExecuteNonQuery();
                        if (rows > 0)
                        {
                            return newId;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd bazy danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return 0;
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        #endregion
    }

    #region Klasy pomocnicze

    public class HodowcaItem
    {
        public string ID { get; set; }
        public string Nazwa { get; set; }
        public string DisplayName => $"{Nazwa} ({ID})";
    }

    public class KierowcaItem
    {
        public int GID { get; set; }
        public string Nazwa { get; set; }
    }

    public class IstniejacaSpecItem
    {
        public int ID { get; set; }
        public int CarLp { get; set; }
        public string Hodowca { get; set; }
        public string Ciagnik { get; set; }
        public string Naczepa { get; set; }
        public int? KierowcaGID { get; set; }
    }

    /// <summary>
    /// Ustawienia cenowe do skopiowania przy duplikacji specyfikacji
    /// </summary>
    public class DuplikatUstawienia
    {
        public string CustomerGID { get; set; }
        public string CustomerRealGID { get; set; }
        public string DostawcaNazwa { get; set; }
        public decimal Cena { get; set; }
        public string TypCeny { get; set; }
        public decimal Ubytek { get; set; }
        public decimal PIK { get; set; }
    }

    #endregion

    #region Klasa DuplicateData (dla menu kontekstowego)

    public partial class NowaSpecyfikacjaWindow
    {
        /// <summary>
        /// Dane do duplikacji specyfikacji z menu kontekstowego
        /// Kopiuje TYLKO ustawienia cenowe hodowcy (bez danych wagowych)
        /// </summary>
        public class DuplicateData
        {
            public string SourceHodowca { get; set; }
            public decimal Cena { get; set; }
            public string TypCeny { get; set; }
            public decimal Ubytek { get; set; }
            public bool PiK { get; set; }
            public string CustomerGID { get; set; }
            public string CustomerRealGID { get; set; }
        }

        /// <summary>
        /// Konstruktor z danymi do duplikacji (z menu kontekstowego)
        /// </summary>
        public NowaSpecyfikacjaWindow(string connString, DateTime? defaultDate, DuplicateData duplicateData)
            : this(connString, defaultDate, duplicateData?.CustomerGID, ConvertToUstawienia(duplicateData))
        {
        }

        private static DuplikatUstawienia ConvertToUstawienia(DuplicateData data)
        {
            if (data == null) return null;

            return new DuplikatUstawienia
            {
                CustomerGID = data.CustomerGID,
                CustomerRealGID = data.CustomerRealGID,
                DostawcaNazwa = data.SourceHodowca,
                Cena = data.Cena,
                TypCeny = data.TypCeny,
                Ubytek = data.Ubytek,
                PIK = data.PiK ? 1 : 0
            };
        }
    }

    #endregion
}
