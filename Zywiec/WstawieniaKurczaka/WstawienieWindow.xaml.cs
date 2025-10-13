using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kalendarz1
{
    public partial class WstawienieWindow : Window
    {
        private const string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        public string UserID { get; set; }
        public double SztWstawienia { get; set; }
        public string Dostawca { get; set; }
        public bool Modyfikacja { get; set; }
        public int LpWstawienia { get; set; }
        public DateTime DataWstawienia { get; set; }

        private List<DostawaRow> dostawyRows = new List<DostawaRow>();
        private List<SeriaWstawienRow> seriaRows = new List<SeriaWstawienRow>();
        private int nextDostawaId = 1;
        private bool trybSerii = false;
        private bool isLoading = false;

        public WstawienieWindow()
        {
            InitializeComponent();
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
                DodajDostawe(35, 2.1, 20);
                DodajDostawe(42, 2.8, 16);
            }
        }

        #region Seria Wstawień

        private void ChkSeria_Checked(object sender, RoutedEventArgs e)
        {
            trybSerii = true;
            panelJednePodstawy.Visibility = Visibility.Collapsed;
            panelSeria.Visibility = Visibility.Visible;

            if (seriaRows.Count == 0)
                DodajWpisSerii();
        }

        private void ChkSeria_Unchecked(object sender, RoutedEventArgs e)
        {
            trybSerii = false;
            panelJednePodstawy.Visibility = Visibility.Visible;
            panelSeria.Visibility = Visibility.Collapsed;
        }

        private void BtnDodajSerie_Click(object sender, RoutedEventArgs e)
        {
            DodajWpisSerii();
        }

        private void DodajWpisSerii()
        {
            var seria = new SeriaWstawienRow { Id = seriaRows.Count + 1 };
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
            if (seriaRows.Count <= 1)
            {
                MessageBox.Show("Musisz mieć przynajmniej jedno wstawienie!", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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

        private void DodajDostawe(int? domyslnaDoba = null, double? domyslnaWaga = null, int? domyslneSztPoj = null)
        {
            // Jeśli są już jakieś dostawy, skopiuj wagę i szt/poj z ostatniej
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
                SztPoj = domyslneSztPoj
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
            dpData.SelectedDateChanged += (s, e) => PrzeliczenieRoznicyDni(dostawa);
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
            txtSztuki.TextChanged += (s, e) => { ObliczSumeSztuk(); ObliczAutaWyliczone(dostawa); };
            Grid.SetColumn(txtSztuki, 7);
            grid.Children.Add(txtSztuki);
            dostawa.TxtSztuki = txtSztuki;

            // Auta (RĘCZNE)
            var txtAutaReczne = new TextBox { Style = (Style)FindResource("HighlightTextBox"), FontSize = 11 };
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

        private void WczytajDostawcow()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT DISTINCT Name FROM dbo.DOSTAWCY ORDER BY Name";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            cmbDostawca.Items.Add(reader["Name"].ToString());
                    }
                }
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

                                panelDaneDostawcy.Visibility = Visibility.Visible;
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
                    string query = "SELECT Uwagi FROM dbo.WstawieniaKurczakow WHERE Lp = @Lp";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Lp", LpWstawienia);
                        object result = cmd.ExecuteScalar();
                        if (result != null && result != DBNull.Value)
                        {
                            txtNotatki.Text = result.ToString();
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
                WczytajDaneDostawcy(cmbDostawca.SelectedItem.ToString());
            }
        }

        private void DpDataWstawienia_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dpDataWstawienia.SelectedDate != null && !isLoading)
            {
                foreach (var dostawa in dostawyRows)
                {
                    PrzeliczenieDoby(dostawa);
                    PrzeliczenieRoznicyDni(dostawa);
                }
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
            if (isLoading || dpDataWstawienia.SelectedDate == null || dostawa.DpData == null || dostawa.TxtDni == null) return;
            if (dostawa.DpData.SelectedDate != null)
            {
                int roznica = (dostawa.DpData.SelectedDate.Value - dpDataWstawienia.SelectedDate.Value).Days;
                dostawa.TxtDni.Text = roznica.ToString();
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

                                string infoNotatki = maNotatki ? $"\n📝 Dodano {iloscNotatek} notatek" : "";
                                MessageBox.Show($"✅ Zapisano {zapisane} wstawień z {dostawyRows.Count} dostawami każde{infoNotatki}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
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
                                    UsunStareDostawy(conn, trans, lpW);
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
                                }

                                foreach (var dostawa in dostawyRows)
                                {
                                    WstawDostaweDb(conn, trans, dostawa, lpW, dpDataWstawienia.SelectedDate.Value, typUmowy, typCeny, bufor, zapiszAuta);
                                    if (maNotatki) iloscNotatek++;
                                }
                                trans.Commit();

                                string infoAuta = zapiszAuta ? "\n\n✅ Auta zostały zapisane" : "\n\n⚠️ Auta NIE zostały zapisane (Wolnyrynek)";
                                string infoNotatki = maNotatki ? $"\n📝 Dodano {iloscNotatek} notatek do dostaw" : "";
                                MessageBox.Show($"✅ Zapisano{infoAuta}{infoNotatki}", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
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

            return true;
        }

        private (string, string, string) JakiTypKontraktu()
        {
            var result = MessageBox.Show("Wolny Rynek?", "Typ", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                var wierny = MessageBox.Show("Wierny?", "Typ", MessageBoxButton.YesNo, MessageBoxImage.Question);
                return wierny == MessageBoxResult.Yes ? ("W.Wolnyrynek", "wolnyrynek", "B.Wolny.") : ("Wolnyrynek", "wolnyrynek", "Do wykupienia");
            }
            else
            {
                string typCeny = WybierzTypCeny();
                return !string.IsNullOrEmpty(typCeny) ? ("Kontrakt", typCeny, "B.Kontr.") : (null, null, null);
            }
        }

        private string WybierzTypCeny()
        {
            var dialog = new Window { Title = "Typ ceny", Width = 250, Height = 200, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this };
            var panel = new StackPanel { Margin = new Thickness(15) };
            string wybrana = null;

            foreach (var opcja in new[] { "łączona", "rolnicza", "wolnyrynek", "ministerialna" })
            {
                var btn = new Button { Content = opcja, Margin = new Thickness(0, 5, 0, 5), Padding = new Thickness(8), Style = (Style)FindResource("PrimaryButton") };
                btn.Click += (s, e) => { wybrana = opcja; dialog.DialogResult = true; };
                panel.Children.Add(btn);
            }

            dialog.Content = panel;
            dialog.ShowDialog();
            return wybrana;
        }

        private long NowyNumerWstawienia(SqlConnection conn, SqlTransaction trans)
        {
            using (var cmd = new SqlCommand("SELECT MAX(Lp) FROM dbo.WstawieniaKurczakow", conn, trans))
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
            using (var cmd = new SqlCommand("SELECT MAX(Lp) FROM dbo.HarmonogramDostaw", conn, trans))
            {
                object v = cmd.ExecuteScalar();
                lp = (v == DBNull.Value) ? 1 : Convert.ToInt64(v) + 1;
            }

            DateTime dataDostawy = dostawa.DpData?.SelectedDate ?? dataWstawienia;
            if (dostawa.TxtDoba != null && int.TryParse(dostawa.TxtDoba.Text, out int doba))
            {
                dataDostawy = dataWstawienia.AddDays(doba);
            }

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

                string wagaText = (dostawa.TxtWaga?.Text ?? "0").Replace(',', '.');
                cmd.Parameters.AddWithValue("@W", decimal.Parse(wagaText, System.Globalization.CultureInfo.InvariantCulture));

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
}