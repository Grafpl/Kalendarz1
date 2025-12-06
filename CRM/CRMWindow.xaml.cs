using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kalendarz1.OfertaCenowa;

namespace Kalendarz1.CRM
{
    public partial class CRMWindow : Window
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string operatorID = "";
        private int aktualnyOdbiorcaID = 0;
        private DataTable dtKontakty;
        private bool isLoading = false;

        private readonly string[] priorytetowePKD = new string[]
        {
            "Sprzedaż detaliczna mięsa i wyrobów z mięsa prowadzona w wyspecjalizowanych sklepach",
            "Przetwarzanie i konserwowanie mięsa z drobiu",
            "Produkcja wyrobów z mięsa, włączając wyroby z mięsa drobiowego",
            "Ubój zwierząt, z wyłączeniem drobiu i królików"
        };

        public string UserID { get; set; }

        public CRMWindow()
        {
            InitializeComponent();
            Loaded += CRMWindow_Loaded;
        }

        private void CRMWindow_Loaded(object sender, RoutedEventArgs e)
        {
            operatorID = UserID;

            // Pokaż przycisk Admin dla uprawnionego użytkownika
            if (operatorID == "11111")
            {
                btnAdmin.Visibility = Visibility.Visible;
            }

            SprawdzIUtworzTabele();
            InicjalizujFiltry();
            WczytajDane();
        }

        #region Inicjalizacja

        private void SprawdzIUtworzTabele()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Historia zmian
                    var cmdHistory = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HistoriaZmianCRM')
                        BEGIN
                            CREATE TABLE HistoriaZmianCRM (
                                ID INT IDENTITY(1,1) PRIMARY KEY,
                                IDOdbiorcy INT NOT NULL,
                                TypZmiany NVARCHAR(100),
                                WartoscStara NVARCHAR(500),
                                WartoscNowa NVARCHAR(500),
                                KtoWykonal NVARCHAR(50),
                                DataZmiany DATETIME DEFAULT GETDATE()
                            )
                        END", conn);
                    cmdHistory.ExecuteNonQuery();

                    // Notatki
                    var cmdNotatki = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NotatkiCRM')
                        BEGIN
                            CREATE TABLE NotatkiCRM (
                                ID INT IDENTITY(1,1) PRIMARY KEY,
                                IDOdbiorcy INT NOT NULL,
                                Tresc NVARCHAR(MAX),
                                KtoDodal NVARCHAR(50),
                                DataUtworzenia DATETIME DEFAULT GETDATE()
                            )
                        END", conn);
                    cmdNotatki.ExecuteNonQuery();

                    // Kolumna DataNastepnegoKontaktu
                    var cmdKolumna = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'DataNastepnegoKontaktu')
                        BEGIN
                            ALTER TABLE OdbiorcyCRM ADD DataNastepnegoKontaktu DATETIME NULL
                        END", conn);
                    cmdKolumna.ExecuteNonQuery();

                    // Kolumna LiczbaProbKontaktu
                    var cmdProby = new SqlCommand(@"
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = 'LiczbaProbKontaktu')
                        BEGIN
                            ALTER TABLE OdbiorcyCRM ADD LiczbaProbKontaktu INT DEFAULT 0
                        END", conn);
                    cmdProby.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji tabel: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void InicjalizujFiltry()
        {
            // Województwa
            cmbWojewodztwo.Items.Clear();
            cmbWojewodztwo.Items.Add(new ComboBoxItem { Content = "Wszystkie woj." });
            cmbWojewodztwo.SelectedIndex = 0;

            // Branże
            cmbBranza.Items.Clear();
            cmbBranza.Items.Add(new ComboBoxItem { Content = "Wszystkie branże" });
            cmbBranza.SelectedIndex = 0;
        }

        #endregion

        #region Ładowanie danych

        private void WczytajDane()
        {
            isLoading = true;
            loadingOverlay.Visibility = Visibility.Visible;

            try
            {
                WczytajKontakty();
                WczytajKPI();
                WczytajRanking();
                WypelnijFiltry();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isLoading = false;
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void WczytajKontakty()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Własne zapytanie z DataNastepnegoKontaktu
                    var cmd = new SqlCommand(@"
                        SELECT
                            o.ID,
                            o.Nazwa as NAZWA,
                            o.KOD,
                            o.MIASTO,
                            o.ULICA,
                            o.Telefon_K as TELEFON_K,
                            o.Email,
                            o.Wojewodztwo,
                            o.Powiat,
                            o.PKD_Opis,
                            ISNULL(o.Status, 'Do zadzwonienia') as Status,
                            o.Imie,
                            o.Nazwisko,
                            o.Stanowisko,
                            o.TelefonDodatkowy,
                            o.DataNastepnegoKontaktu,
                            ISNULL(o.LiczbaProbKontaktu, 0) as LiczbaProbKontaktu,
                            (SELECT TOP 1 DataZmiany FROM HistoriaZmianCRM WHERE IDOdbiorcy = o.ID ORDER BY DataZmiany DESC) as OstatniaZmiana,
                            CASE
                                WHEN o.DataNastepnegoKontaktu IS NULL THEN 0
                                WHEN CAST(o.DataNastepnegoKontaktu AS DATE) < CAST(GETDATE() AS DATE) THEN 1
                                WHEN CAST(o.DataNastepnegoKontaktu AS DATE) = CAST(GETDATE() AS DATE) THEN 2
                                ELSE 3
                            END as PriorytetKontaktu,
                            CASE
                                WHEN o.PKD_Opis IN (
                                    'Sprzedaż detaliczna mięsa i wyrobów z mięsa prowadzona w wyspecjalizowanych sklepach',
                                    'Przetwarzanie i konserwowanie mięsa z drobiu',
                                    'Produkcja wyrobów z mięsa, włączając wyroby z mięsa drobiowego',
                                    'Ubój zwierząt, z wyłączeniem drobiu i królików'
                                ) THEN 1 ELSE 0
                            END as CzyPriorytetowaBranza
                        FROM OdbiorcyCRM o
                        LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
                        WHERE (w.OperatorID = @OperatorID OR w.OperatorID IS NULL)
                            AND ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')
                        ORDER BY
                            CASE
                                WHEN o.DataNastepnegoKontaktu IS NULL THEN 2
                                WHEN CAST(o.DataNastepnegoKontaktu AS DATE) < CAST(GETDATE() AS DATE) THEN 0
                                WHEN CAST(o.DataNastepnegoKontaktu AS DATE) = CAST(GETDATE() AS DATE) THEN 1
                                ELSE 3
                            END,
                            o.DataNastepnegoKontaktu ASC,
                            o.Nazwa", conn);

                    cmd.Parameters.AddWithValue("@OperatorID", operatorID);

                    var adapter = new SqlDataAdapter(cmd);
                    dtKontakty = new DataTable();
                    adapter.Fill(dtKontakty);

                    // Zamień "Nowy" na "Do zadzwonienia"
                    foreach (DataRow row in dtKontakty.Rows)
                    {
                        string status = row["Status"]?.ToString() ?? "";
                        if (string.IsNullOrWhiteSpace(status) || status == "Nowy")
                            row["Status"] = "Do zadzwonienia";
                    }

                    dgKontakty.ItemsSource = dtKontakty.DefaultView;
                    txtLiczbaWynikow.Text = $"{dtKontakty.Rows.Count} wyników";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania kontaktów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajKPI()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Statystyki - dziś na podstawie DataNastepnegoKontaktu
                    var cmd = new SqlCommand(@"
                        SELECT
                            -- Dziś do zadzwonienia (na podstawie daty kontaktu)
                            ISNULL(SUM(CASE
                                WHEN DataNastepnegoKontaktu IS NOT NULL
                                    AND CAST(DataNastepnegoKontaktu AS DATE) <= CAST(GETDATE() AS DATE)
                                THEN 1 ELSE 0 END), 0) as DzisDoZadzwonienia,
                            -- Zaległe (przeterminowane)
                            ISNULL(SUM(CASE
                                WHEN DataNastepnegoKontaktu IS NOT NULL
                                    AND CAST(DataNastepnegoKontaktu AS DATE) < CAST(GETDATE() AS DATE)
                                THEN 1 ELSE 0 END), 0) as Zalegle,
                            -- Próby kontaktu
                            ISNULL(SUM(CASE WHEN Status = 'Próba kontaktu' THEN 1 ELSE 0 END), 0) as ProbaKontaktu,
                            -- Nawiązane kontakty
                            ISNULL(SUM(CASE WHEN Status = 'Nawiązano kontakt' OR Status = 'Zgoda na dalszy kontakt' THEN 1 ELSE 0 END), 0) as Nawiazane,
                            -- Do wysłania oferty
                            ISNULL(SUM(CASE WHEN Status = 'Do wysłania oferta' THEN 1 ELSE 0 END), 0) as DoOferty,
                            -- Razem aktywnych
                            COUNT(*) as Razem
                        FROM OdbiorcyCRM o
                        LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
                        WHERE (w.OperatorID = @OperatorID OR w.OperatorID IS NULL)
                            AND ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)', 'Nie zainteresowany')", conn);

                    cmd.Parameters.AddWithValue("@OperatorID", operatorID);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int dzis = Convert.ToInt32(reader["DzisDoZadzwonienia"]);
                            int zalegle = Convert.ToInt32(reader["Zalegle"]);

                            // Pokazuj zaległe w czerwonym jeśli są
                            if (zalegle > 0)
                                txtKpiDzisiaj.Text = $"{dzis} ({zalegle} zaległych)";
                            else
                                txtKpiDzisiaj.Text = dzis.ToString();

                            txtKpiProby.Text = reader["ProbaKontaktu"].ToString();
                            txtKpiNawiazane.Text = reader["Nawiazane"].ToString();
                            txtKpiOferty.Text = reader["DoOferty"].ToString();
                            txtKpiRazem.Text = reader["Razem"].ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignoruj błędy KPI - nie są krytyczne
                System.Diagnostics.Debug.WriteLine($"Błąd KPI: {ex.Message}");
            }
        }

        private void WczytajRanking()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        SELECT TOP 10
                            ISNULL(
                                CASE
                                    WHEN CHARINDEX(' ', o.Name) > 0
                                    THEN LEFT(o.Name, CHARINDEX(' ', o.Name) - 1) + ' ' + LEFT(SUBSTRING(o.Name, CHARINDEX(' ', o.Name) + 1, LEN(o.Name)), 1) + '.'
                                    ELSE o.Name
                                END,
                                'ID: ' + CAST(h.KtoWykonal AS NVARCHAR)
                            ) as Operator,
                            SUM(CASE WHEN h.WartoscNowa = 'Próba kontaktu' THEN 1 ELSE 0 END) as Proby,
                            SUM(CASE WHEN h.WartoscNowa = 'Nawiązano kontakt' THEN 1 ELSE 0 END) as Kontakt,
                            SUM(CASE WHEN h.WartoscNowa = 'Zgoda na dalszy kontakt' THEN 1 ELSE 0 END) as Zgoda,
                            COUNT(*) as Suma
                        FROM HistoriaZmianCRM h
                        LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                        WHERE h.TypZmiany = 'Zmiana statusu'
                            AND h.WartoscStara != h.WartoscNowa
                            AND h.WartoscNowa != 'Nowy'
                        GROUP BY h.KtoWykonal, o.Name
                        ORDER BY COUNT(*) DESC", conn);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    dgRanking.ItemsSource = dt.DefaultView;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd rankingu: {ex.Message}");
            }
        }

        private void WypelnijFiltry()
        {
            if (dtKontakty == null) return;

            // Województwa
            var wojewodztwa = dtKontakty.AsEnumerable()
                .Select(r => r.Field<string>("Wojewodztwo"))
                .Where(w => !string.IsNullOrEmpty(w))
                .Distinct()
                .OrderBy(w => w)
                .ToList();

            cmbWojewodztwo.Items.Clear();
            cmbWojewodztwo.Items.Add(new ComboBoxItem { Content = "Wszystkie woj." });
            foreach (var woj in wojewodztwa)
            {
                cmbWojewodztwo.Items.Add(new ComboBoxItem { Content = woj });
            }
            cmbWojewodztwo.SelectedIndex = 0;

            // Branże
            var branze = dtKontakty.AsEnumerable()
                .Select(r => r.Field<string>("PKD_Opis"))
                .Where(b => !string.IsNullOrEmpty(b))
                .Distinct()
                .OrderBy(b => b)
                .ToList();

            cmbBranza.Items.Clear();
            cmbBranza.Items.Add(new ComboBoxItem { Content = "Wszystkie branże" });
            foreach (var branza in branze)
            {
                cmbBranza.Items.Add(new ComboBoxItem { Content = branza });
            }
            cmbBranza.SelectedIndex = 0;
        }

        private void WczytajNotatki(int idOdbiorcy)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                        SELECT TOP 20
                            n.Tresc,
                            n.DataUtworzenia,
                            ISNULL(
                                CASE
                                    WHEN CHARINDEX(' ', o.Name) > 0
                                    THEN LEFT(o.Name, CHARINDEX(' ', o.Name) - 1) + ' ' + LEFT(SUBSTRING(o.Name, CHARINDEX(' ', o.Name) + 1, LEN(o.Name)), 1) + '.'
                                    ELSE o.Name
                                END,
                                'ID: ' + CAST(n.KtoDodal AS NVARCHAR)
                            ) as Operator
                        FROM NotatkiCRM n
                        LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                        WHERE n.IDOdbiorcy = @id
                        ORDER BY n.DataUtworzenia DESC", conn);

                    cmd.Parameters.AddWithValue("@id", idOdbiorcy);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    var notatki = new ObservableCollection<NotatkaCRM>();
                    foreach (DataRow row in dt.Rows)
                    {
                        notatki.Add(new NotatkaCRM
                        {
                            Tresc = row["Tresc"]?.ToString() ?? "",
                            DataUtworzenia = row["DataUtworzenia"] as DateTime? ?? DateTime.Now,
                            Operator = row["Operator"]?.ToString() ?? ""
                        });
                    }

                    listaNotatek.ItemsSource = notatki;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd notatek: {ex.Message}");
            }
        }

        #endregion

        #region Filtrowanie

        private void ZastosujFiltry()
        {
            if (dtKontakty == null || isLoading) return;

            var filtry = new List<string>();

            // Status
            if (cmbStatus.SelectedIndex > 0)
            {
                var status = (cmbStatus.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "Błędny rekord")
                        filtry.Add("Status LIKE 'Błędny%'");
                    else
                        filtry.Add($"Status = '{status}'");
                }
            }

            // Województwo
            if (cmbWojewodztwo.SelectedIndex > 0)
            {
                var woj = (cmbWojewodztwo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (!string.IsNullOrEmpty(woj))
                    filtry.Add($"Wojewodztwo = '{woj}'");
            }

            // Branża
            if (cmbBranza.SelectedIndex > 0)
            {
                var branza = (cmbBranza.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Replace("'", "''");
                if (!string.IsNullOrEmpty(branza))
                    filtry.Add($"PKD_Opis = '{branza}'");
            }

            // Szukaj
            var szukaj = txtSzukaj.Text?.Trim().Replace("'", "''");
            if (!string.IsNullOrEmpty(szukaj))
            {
                filtry.Add($"NAZWA LIKE '%{szukaj}%'");
            }

            dtKontakty.DefaultView.RowFilter = string.Join(" AND ", filtry);
            txtLiczbaWynikow.Text = $"{dtKontakty.DefaultView.Count} wyników";
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            ZastosujFiltry();
        }

        private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ZastosujFiltry();
        }

        private void CmbWojewodztwo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ZastosujFiltry();
        }

        private void CmbBranza_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ZastosujFiltry();
        }

        #endregion

        #region Obsługa wyboru kontaktu

        private void DgKontakty_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgKontakty.SelectedItem == null) return;

            var row = (DataRowView)dgKontakty.SelectedItem;
            aktualnyOdbiorcaID = Convert.ToInt32(row["ID"]);

            // Aktualizuj panel klienta
            txtKlientNazwa.Text = row["NAZWA"]?.ToString() ?? "-";
            txtKlientStatus.Text = row["Status"]?.ToString() ?? "-";
            txtKlientTelefon.Text = row["TELEFON_K"]?.ToString() ?? "-";
            txtKlientEmail.Text = row["Email"]?.ToString() ?? "-";

            var ulica = row["ULICA"]?.ToString() ?? "";
            var kod = row["KOD"]?.ToString() ?? "";
            var miasto = row["MIASTO"]?.ToString() ?? "";
            txtKlientAdres.Text = $"{ulica}, {kod} {miasto}".Trim().TrimStart(',').Trim();

            // Wczytaj notatki
            WczytajNotatki(aktualnyOdbiorcaID);

            // Ustaw kolor badge statusu
            UstawKolorStatusu(row["Status"]?.ToString() ?? "");

            // Wyświetl datę następnego kontaktu
            var dataNastepnego = row["DataNastepnegoKontaktu"] as DateTime?;
            if (dataNastepnego.HasValue)
            {
                var data = dataNastepnego.Value;
                if (data.Date == DateTime.Today)
                {
                    txtKlientNastepnyKontakt.Text = "DZIŚ";
                    panelNastepnyKontakt.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#DBEAFE"));
                }
                else if (data.Date < DateTime.Today)
                {
                    txtKlientNastepnyKontakt.Text = $"ZALEGŁY ({data:dd.MM.yyyy})";
                    panelNastepnyKontakt.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FEE2E2"));
                }
                else
                {
                    string dzienTygodnia = data.ToString("dddd", new System.Globalization.CultureInfo("pl-PL"));
                    dzienTygodnia = char.ToUpper(dzienTygodnia[0]) + dzienTygodnia.Substring(1);
                    txtKlientNastepnyKontakt.Text = $"{dzienTygodnia}, {data:dd.MM.yyyy}";
                    panelNastepnyKontakt.Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0F9FF"));
                }
            }
            else
            {
                txtKlientNastepnyKontakt.Text = "Nie ustawiono";
                panelNastepnyKontakt.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F9FAFB"));
            }
        }

        private void UstawKolorStatusu(string status)
        {
            switch (status)
            {
                case "Do zadzwonienia":
                    badgeKlientStatus.Background = (System.Windows.Media.Brush)FindResource("ColorBorder");
                    break;
                case "Próba kontaktu":
                    badgeKlientStatus.Background = (System.Windows.Media.Brush)FindResource("ColorWarningLight");
                    break;
                case "Nawiązano kontakt":
                    badgeKlientStatus.Background = (System.Windows.Media.Brush)FindResource("ColorPrimaryLight");
                    break;
                case "Zgoda na dalszy kontakt":
                case "Do wysłania oferta":
                    badgeKlientStatus.Background = (System.Windows.Media.Brush)FindResource("ColorSuccessLight");
                    break;
                case "Nie zainteresowany":
                case "Poprosił o usunięcie":
                    badgeKlientStatus.Background = (System.Windows.Media.Brush)FindResource("ColorDangerLight");
                    break;
                default:
                    badgeKlientStatus.Background = (System.Windows.Media.Brush)FindResource("ColorBorder");
                    break;
            }
        }

        private void DgKontakty_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MenuEdytuj_Click(sender, e);
        }

        #endregion

        #region Przyciski górne

        private void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            var form = new FormDodajOdbiorce(connectionString, operatorID);
            if (form.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                WczytajDane();
            }
        }

        private void BtnMapa_Click(object sender, RoutedEventArgs e)
        {
            var form = new FormMapaWojewodztwa(connectionString, operatorID);
            form.ShowDialog();
        }

        private void BtnZadania_Click(object sender, RoutedEventArgs e)
        {
            var form = new FormZadania(connectionString, operatorID);
            form.ShowDialog();
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            WczytajDane();
        }

        private void BtnAdmin_Click(object sender, RoutedEventArgs e)
        {
            var panel = new PanelAdministracyjny(connectionString);
            panel.ShowDialog();
            WczytajDane();
        }

        #endregion

        #region Przyciski panelu klienta

        private void BtnKlientZadzwon_Click(object sender, RoutedEventArgs e)
        {
            var telefon = txtKlientTelefon.Text?.Replace(" ", "").Replace("-", "");
            if (!string.IsNullOrEmpty(telefon) && telefon != "-")
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"tel:{telefon}",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    Clipboard.SetText(telefon);
                    MessageBox.Show($"Numer {telefon} skopiowany do schowka.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void TxtKlientTelefon_Click(object sender, MouseButtonEventArgs e)
        {
            BtnKlientZadzwon_Click(sender, null);
        }

        private void BtnKlientOferta_Click(object sender, RoutedEventArgs e)
        {
            if (aktualnyOdbiorcaID <= 0) return;

            if (dgKontakty.SelectedItem is DataRowView rowView)
            {
                UtworzOferte(rowView);
            }
        }

        private void BtnKlientEdytuj_Click(object sender, RoutedEventArgs e)
        {
            MenuEdytuj_Click(sender, e);
        }

        private void BtnZmienDate_Click(object sender, RoutedEventArgs e)
        {
            if (aktualnyOdbiorcaID <= 0) return;
            MenuUstawDate_Click(sender, e);
        }

        private void BtnDodajNotatke_Click(object sender, RoutedEventArgs e)
        {
            if (aktualnyOdbiorcaID <= 0)
            {
                MessageBox.Show("Wybierz kontakt z listy.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var tresc = txtNowaNotatka.Text?.Trim();
            if (string.IsNullOrEmpty(tresc))
            {
                MessageBox.Show("Wpisz treść notatki.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        var cmdNotatka = new SqlCommand(@"
                            INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal)
                            VALUES (@id, @tresc, @kto)", conn, transaction);
                        cmdNotatka.Parameters.AddWithValue("@id", aktualnyOdbiorcaID);
                        cmdNotatka.Parameters.AddWithValue("@tresc", tresc);
                        cmdNotatka.Parameters.AddWithValue("@kto", operatorID);
                        cmdNotatka.ExecuteNonQuery();

                        var cmdLog = new SqlCommand(@"
                            INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany)
                            VALUES (@id, 'Dodanie notatki', @wartosc, @kto, GETDATE())", conn, transaction);
                        cmdLog.Parameters.AddWithValue("@id", aktualnyOdbiorcaID);
                        cmdLog.Parameters.AddWithValue("@wartosc", tresc.Length > 100 ? tresc.Substring(0, 100) + "..." : tresc);
                        cmdLog.Parameters.AddWithValue("@kto", operatorID);
                        cmdLog.ExecuteNonQuery();

                        transaction.Commit();
                    }
                }

                txtNowaNotatka.Clear();
                WczytajNotatki(aktualnyOdbiorcaID);
                MessageBox.Show("Notatka dodana!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania notatki: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Menu kontekstowe

        private void MenuZadzwon_Click(object sender, RoutedEventArgs e)
        {
            BtnKlientZadzwon_Click(sender, e);
        }

        private void MenuEdytuj_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem == null) return;

            var row = (DataRowView)dgKontakty.SelectedItem;
            int id = Convert.ToInt32(row["ID"]);
            string nazwa = row["NAZWA"]?.ToString() ?? "";

            var okno = new EdycjaKontaktuWindow
            {
                KlientID = id,
                KlientNazwa = nazwa,
                OperatorID = operatorID
            };

            if (okno.ShowDialog() == true && okno.ZapisanoZmiany)
            {
                WczytajDane();
            }
        }

        private void MenuOferta_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView rowView)
            {
                UtworzOferte(rowView);
            }
        }

        private void UtworzOferte(DataRowView rowView)
        {
            try
            {
                string nazwa = rowView["NAZWA"]?.ToString() ?? "";
                string kod = rowView["KOD"]?.ToString() ?? "";
                string miasto = rowView["MIASTO"]?.ToString() ?? "";
                string ulica = rowView["ULICA"]?.ToString() ?? "";
                string telefon = rowView["TELEFON_K"]?.ToString() ?? "";
                string imie = rowView.Row.Table.Columns.Contains("Imie") ? rowView["Imie"]?.ToString() ?? "" : "";
                string nazwisko = rowView.Row.Table.Columns.Contains("Nazwisko") ? rowView["Nazwisko"]?.ToString() ?? "" : "";
                int idCRM = Convert.ToInt32(rowView["ID"]);

                var klient = new KlientOferta
                {
                    Id = idCRM.ToString(),
                    Nazwa = nazwa,
                    NIP = "",
                    Adres = ulica,
                    KodPocztowy = kod,
                    Miejscowosc = miasto,
                    Telefon = telefon,
                    OsobaKontaktowa = $"{imie} {nazwisko}".Trim(),
                    CzyReczny = true
                };

                var ofertaWindow = new OfertaHandlowaWindow(klient, operatorID);
                ofertaWindow.UserID = operatorID;
                ofertaWindow.ShowDialog();

                // Po zamknięciu oferty - zapytaj czy zmienić status
                var currentStatus = rowView["Status"]?.ToString() ?? "";
                if (currentStatus == "Do wysłania oferta")
                {
                    var result = MessageBox.Show(
                        $"Oferta została utworzona dla:\n{nazwa}\n\nCzy zmienić status klienta na 'Zgoda na dalszy kontakt'?",
                        "Zmiana statusu",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        ZmienStatus(idCRM, "Zgoda na dalszy kontakt", currentStatus);
                        WczytajDane();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd tworzenia oferty: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuGoogle_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView row)
            {
                var nazwa = row["NAZWA"]?.ToString();
                if (!string.IsNullOrEmpty(nazwa))
                {
                    var url = $"https://www.google.com/search?q={Uri.EscapeDataString(nazwa)}";
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
            }
        }

        private void MenuTrasa_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView row)
            {
                var ulica = row["ULICA"]?.ToString() ?? "";
                var miasto = row["MIASTO"]?.ToString() ?? "";
                var kod = row["KOD"]?.ToString() ?? "";
                var adres = $"{ulica}, {kod} {miasto}";
                var start = "Koziołki 40, 95-061 Dmosin";

                if (!string.IsNullOrEmpty(ulica) && !string.IsNullOrEmpty(miasto))
                {
                    var url = $"https://www.google.com/maps/dir/{Uri.EscapeDataString(start)}/{Uri.EscapeDataString(adres)}";
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
            }
        }

        private void MenuStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && dgKontakty.SelectedItem is DataRowView row)
            {
                var nowyStatus = menuItem.Tag?.ToString();
                var staryStatus = row["Status"]?.ToString() ?? "";
                var id = Convert.ToInt32(row["ID"]);

                if (!string.IsNullOrEmpty(nowyStatus) && nowyStatus != staryStatus)
                {
                    ZmienStatus(id, nowyStatus, staryStatus);
                    WczytajDane();
                }
            }
        }

        private void ZmienStatus(int idOdbiorcy, string nowyStatus, string staryStatus)
        {
            try
            {
                // Określ domyślną datę następnego kontaktu na podstawie statusu
                DateTime? dataNastepnegoKontaktu = null;
                bool inkrementujProby = false;

                switch (nowyStatus)
                {
                    case "Próba kontaktu":
                        // Nie odebrał - zadzwoń za 2 dni
                        dataNastepnegoKontaktu = DateTime.Today.AddDays(2);
                        inkrementujProby = true;
                        break;
                    case "Nawiązano kontakt":
                        // Rozmowa OK - follow-up za tydzień
                        dataNastepnegoKontaktu = DateTime.Today.AddDays(7);
                        break;
                    case "Zgoda na dalszy kontakt":
                        // Zainteresowany - szybki follow-up za 3 dni
                        dataNastepnegoKontaktu = DateTime.Today.AddDays(3);
                        break;
                    case "Do wysłania oferta":
                        // Wyślij ofertę dzisiaj, follow-up za 2 dni
                        dataNastepnegoKontaktu = DateTime.Today.AddDays(2);
                        break;
                    case "Nie zainteresowany":
                        // Może za pół roku
                        dataNastepnegoKontaktu = DateTime.Today.AddMonths(6);
                        break;
                }

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        // Aktualizuj status i datę następnego kontaktu
                        var cmdUpdate = new SqlCommand(@"
                            UPDATE OdbiorcyCRM SET
                                Status = @status,
                                DataNastepnegoKontaktu = @dataKontaktu,
                                LiczbaProbKontaktu = CASE WHEN @inkrementuj = 1 THEN ISNULL(LiczbaProbKontaktu, 0) + 1 ELSE LiczbaProbKontaktu END
                            WHERE ID = @id", conn, transaction);
                        cmdUpdate.Parameters.AddWithValue("@id", idOdbiorcy);
                        cmdUpdate.Parameters.AddWithValue("@status", nowyStatus);
                        cmdUpdate.Parameters.AddWithValue("@dataKontaktu", dataNastepnegoKontaktu.HasValue ? (object)dataNastepnegoKontaktu.Value : DBNull.Value);
                        cmdUpdate.Parameters.AddWithValue("@inkrementuj", inkrementujProby ? 1 : 0);
                        cmdUpdate.ExecuteNonQuery();

                        var cmdLog = new SqlCommand(@"
                            INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscStara, WartoscNowa, KtoWykonal, DataZmiany)
                            VALUES (@idOdbiorcy, 'Zmiana statusu', @stara, @nowa, @kto, GETDATE())", conn, transaction);
                        cmdLog.Parameters.AddWithValue("@idOdbiorcy", idOdbiorcy);
                        cmdLog.Parameters.AddWithValue("@stara", staryStatus);
                        cmdLog.Parameters.AddWithValue("@nowa", nowyStatus);
                        cmdLog.Parameters.AddWithValue("@kto", operatorID);
                        cmdLog.ExecuteNonQuery();

                        transaction.Commit();
                    }
                }

                // Pokaż informację o następnym kontakcie
                if (dataNastepnegoKontaktu.HasValue)
                {
                    MessageBox.Show($"Następny kontakt zaplanowany na: {dataNastepnegoKontaktu.Value:dd.MM.yyyy}",
                        "Zaplanowano kontakt", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zmiany statusu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UstawDateNastepnegoKontaktu(int idOdbiorcy, DateTime data)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("UPDATE OdbiorcyCRM SET DataNastepnegoKontaktu = @data WHERE ID = @id", conn);
                    cmd.Parameters.AddWithValue("@id", idOdbiorcy);
                    cmd.Parameters.AddWithValue("@data", data);
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ustawiania daty: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MenuUstawDate_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView row)
            {
                int id = Convert.ToInt32(row["ID"]);
                string nazwa = row["NAZWA"]?.ToString() ?? "";

                var dialog = new UstawDateKontaktuDialog(nazwa);
                if (dialog.ShowDialog() == true && dialog.WybranaData.HasValue)
                {
                    UstawDateNastepnegoKontaktu(id, dialog.WybranaData.Value);
                    WczytajDane();
                }
            }
        }

        private void MenuKopiujTelefon_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView row)
            {
                var telefon = row["TELEFON_K"]?.ToString();
                if (!string.IsNullOrEmpty(telefon))
                {
                    Clipboard.SetText(telefon);
                }
            }
        }

        private void MenuKopiujEmail_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView row)
            {
                var email = row["Email"]?.ToString();
                if (!string.IsNullOrEmpty(email))
                {
                    Clipboard.SetText(email);
                }
            }
        }

        private void MenuKopiujAdres_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView row)
            {
                var ulica = row["ULICA"]?.ToString() ?? "";
                var kod = row["KOD"]?.ToString() ?? "";
                var miasto = row["MIASTO"]?.ToString() ?? "";
                var adres = $"{ulica}, {kod} {miasto}".Trim().TrimStart(',').Trim();

                if (!string.IsNullOrEmpty(adres))
                {
                    Clipboard.SetText(adres);
                }
            }
        }

        #endregion
    }

    // Model notatki
    public class NotatkaCRM
    {
        public string Tresc { get; set; }
        public DateTime DataUtworzenia { get; set; }
        public string Operator { get; set; }
    }
}
