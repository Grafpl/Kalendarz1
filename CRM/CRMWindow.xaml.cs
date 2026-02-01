using Kalendarz1.OfertaCenowa;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Kalendarz1.CRM
{
    public partial class CRMWindow : Window
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string operatorID = "";
        private int aktualnyOdbiorcaID = 0;
        private DataTable dtKontakty;
        private bool isLoading = false;

        public string UserID { get; set; }

        public CRMWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            Loaded += CRMWindow_Loaded;
        }

        private void CRMWindow_Loaded(object sender, RoutedEventArgs e)
        {
            operatorID = UserID;
            if (operatorID != "11111")
            {
                if (btnManager != null) btnManager.Visibility = Visibility.Collapsed;
                if (btnAdmin != null) btnAdmin.Visibility = Visibility.Collapsed;
            }
            InicjalizujFiltry();
            WczytajDane();
        }

        #region Ładowanie Danych
        private void InicjalizujFiltry()
        {
            cmbStatus.Items.Clear();
            cmbStatus.Items.Add(new ComboBoxItem { Content = "Wszystkie statusy", IsSelected = true });
            cmbStatus.Items.Add(new ComboBoxItem { Content = "Do zadzwonienia" });
            cmbStatus.Items.Add(new ComboBoxItem { Content = "Próba kontaktu" });
            cmbStatus.Items.Add(new ComboBoxItem { Content = "Nawiązano kontakt" });
            cmbStatus.Items.Add(new ComboBoxItem { Content = "Zgoda na dalszy kontakt" });
            cmbStatus.Items.Add(new ComboBoxItem { Content = "Do wysłania oferta" });
            cmbStatus.Items.Add(new ComboBoxItem { Content = "Nie zainteresowany" });

            cmbWojewodztwo.Items.Clear(); cmbWojewodztwo.Items.Add(new ComboBoxItem { Content = "Wszystkie woj.", IsSelected = true });
            cmbBranza.Items.Clear(); cmbBranza.Items.Add(new ComboBoxItem { Content = "Wszystkie branże", IsSelected = true });
        }

        private void WczytajDane()
        {
            isLoading = true;
            if (loadingOverlay != null) loadingOverlay.Visibility = Visibility.Visible;
            try
            {
                WczytajKontakty();
                WczytajKPI();
                WczytajRanking();
                WypelnijFiltryDynamiczne();
                ObliczTargetDnia();
            }
            catch (Exception ex) { MessageBox.Show($"Błąd: {ex.Message}"); }
            finally
            {
                isLoading = false;
                if (loadingOverlay != null) loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        // Baza firmy - współrzędne do obliczania dystansu
        private const double BazaLat = 51.907335;
        private const double BazaLng = 19.678605;

        private void WczytajKontakty()
        {
            bool tylkoMoje = chkTylkoMoje?.IsChecked == true;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Różne zapytanie w zależności od filtra "Tylko moi"
                string whereClause = tylkoMoje
                    ? "WHERE w.OperatorID = @OperatorID AND ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')"
                    : "WHERE (w.OperatorID = @OperatorID OR w.OperatorID IS NULL) AND ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')";

                var cmd = new SqlCommand($@"
                    SELECT o.ID, o.Nazwa as NAZWA, o.KOD, o.MIASTO, o.ULICA, o.Telefon_K as TELEFON_K, o.Email,
                        o.Wojewodztwo, o.PKD_Opis, o.Tagi, ISNULL(o.Status, 'Do zadzwonienia') as Status, o.DataNastepnegoKontaktu,
                        (SELECT TOP 1 DataZmiany FROM HistoriaZmianCRM WHERE IDOdbiorcy = o.ID ORDER BY DataZmiany DESC) as OstatniaZmiana,
                        (SELECT TOP 1 ISNULL(op.Name, h.KtoDodal) FROM NotatkiCRM h LEFT JOIN operators op ON h.KtoDodal = CAST(op.ID AS NVARCHAR) WHERE h.IDOdbiorcy = o.ID ORDER BY h.DataUtworzenia DESC) as OstatniHandlowiec,
                        kp.Latitude, kp.Longitude
                    FROM OdbiorcyCRM o
                    LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
                    LEFT JOIN KodyPocztowe kp ON o.KOD = kp.Kod
                    {whereClause}
                    ORDER BY CASE WHEN o.DataNastepnegoKontaktu IS NULL THEN 1 ELSE 0 END, o.DataNastepnegoKontaktu ASC", conn);

                cmd.Parameters.AddWithValue("@OperatorID", operatorID);
                var adapter = new SqlDataAdapter(cmd);
                dtKontakty = new DataTable();
                adapter.Fill(dtKontakty);

                if (!dtKontakty.Columns.Contains("CzyZaniedbany")) dtKontakty.Columns.Add("CzyZaniedbany", typeof(bool));
                if (!dtKontakty.Columns.Contains("MaTagi")) dtKontakty.Columns.Add("MaTagi", typeof(bool));
                if (!dtKontakty.Columns.Contains("Km")) dtKontakty.Columns.Add("Km", typeof(string));

                foreach (DataRow r in dtKontakty.Rows)
                {
                    if (r["OstatniaZmiana"] != DBNull.Value)
                    {
                        var data = (DateTime)r["OstatniaZmiana"];
                        if ((DateTime.Now - data).TotalDays > 30) r["CzyZaniedbany"] = true;
                        else r["CzyZaniedbany"] = false;
                    }
                    else r["CzyZaniedbany"] = false;
                    r["MaTagi"] = !string.IsNullOrEmpty(r["Tagi"]?.ToString());

                    // Wojewodztwo z malej litery
                    if (r["Wojewodztwo"] != DBNull.Value && !string.IsNullOrEmpty(r["Wojewodztwo"].ToString()))
                        r["Wojewodztwo"] = r["Wojewodztwo"].ToString().ToLower();

                    // Oblicz dystans
                    if (r["Latitude"] != DBNull.Value && r["Longitude"] != DBNull.Value)
                    {
                        double lat = Convert.ToDouble(r["Latitude"]);
                        double lng = Convert.ToDouble(r["Longitude"]);
                        double km = ObliczDystans(BazaLat, BazaLng, lat, lng);
                        r["Km"] = km.ToString("0");
                    }
                    else r["Km"] = "-";
                }
                dgKontakty.ItemsSource = dtKontakty.DefaultView;
                if (txtLiczbaWynikow != null) txtLiczbaWynikow.Text = $"{dtKontakty.Rows.Count} klientów";
            }
        }

        private double ObliczDystans(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Promień ziemi w km
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private void WczytajKPI()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                var cmd = new SqlCommand(@"SELECT SUM(CASE WHEN CAST(DataNastepnegoKontaktu AS DATE) = CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) as Dzis,
                        SUM(CASE WHEN CAST(DataNastepnegoKontaktu AS DATE) < CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) as Zalegle,
                        SUM(CASE WHEN Status = 'Do wysłania oferta' THEN 1 ELSE 0 END) as Oferty
                    FROM OdbiorcyCRM LEFT JOIN WlascicieleOdbiorcow w ON ID = w.IDOdbiorcy WHERE (w.OperatorID = @Op OR w.OperatorID IS NULL)", conn);
                cmd.Parameters.AddWithValue("@Op", operatorID);
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        if (txtKpiDzisiaj != null) txtKpiDzisiaj.Text = reader["Dzis"].ToString();
                        if (txtKpiZalegle != null) txtKpiZalegle.Text = reader["Zalegle"].ToString();
                        if (txtKpiOferty != null) txtKpiOferty.Text = reader["Oferty"].ToString();
                    }
                }
            }
        }

        private void ObliczTargetDnia()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT COUNT(*) FROM HistoriaZmianCRM WHERE KtoWykonal = @op AND CAST(DataZmiany AS DATE) = CAST(GETDATE() AS DATE)", conn);
                    cmd.Parameters.AddWithValue("@op", operatorID);
                    int wykonane = (int)cmd.ExecuteScalar();
                    int CEL = 15;

                    if (txtTargetInfo != null) txtTargetInfo.Text = $"{wykonane}/{CEL}";
                    if (pbTarget != null)
                    {
                        pbTarget.Maximum = CEL;
                        pbTarget.Value = wykonane;
                    }
                }
            }
            catch { }
        }

        private void WczytajRanking(bool wszystkieDni = false)
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    // Liczymy wszystko oprócz 'Do zadzwonienia'
                    string whereDate = wszystkieDni
                        ? "WHERE h.TypZmiany = 'Zmiana statusu' AND h.WartoscNowa <> 'Do zadzwonienia'"
                        : "WHERE h.DataZmiany > DATEADD(day, -30, GETDATE()) AND h.TypZmiany = 'Zmiana statusu' AND h.WartoscNowa <> 'Do zadzwonienia'";

                    var cmd = new SqlCommand($@"
                        SELECT TOP 10 ROW_NUMBER() OVER (ORDER BY COUNT(*) DESC) as Pozycja,
                            ISNULL(o.Name, 'ID: ' + h.KtoWykonal) as Operator,
                            COUNT(*) as Suma,
                            SUM(CASE WHEN WartoscNowa = 'Próba kontaktu' THEN 1 ELSE 0 END) as Proby,
                            SUM(CASE WHEN WartoscNowa = 'Nawiązano kontakt' THEN 1 ELSE 0 END) as Nawiazano,
                            SUM(CASE WHEN WartoscNowa = 'Zgoda na dalszy kontakt' THEN 1 ELSE 0 END) as Zgoda,
                            SUM(CASE WHEN WartoscNowa = 'Do wysłania oferta' THEN 1 ELSE 0 END) as Oferty,
                            SUM(CASE WHEN WartoscNowa = 'Nie zainteresowany' THEN 1 ELSE 0 END) as NieZainteresowany
                        FROM HistoriaZmianCRM h LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                        {whereDate}
                        GROUP BY h.KtoWykonal, o.Name ORDER BY Suma DESC", conn);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    if (listaRanking != null) listaRanking.ItemsSource = dt.DefaultView;

                    // Aktualizuj tytuł
                    if (txtRankingTytul != null)
                        txtRankingTytul.Text = wszystkieDni ? "RANKING AKTYWNOŚCI (Wszystkie dni)" : "RANKING AKTYWNOŚCI (Ostatnie 30 dni)";
                }
            }
            catch { }
        }

        private void RbOkresRankingu_Checked(object sender, RoutedEventArgs e)
        {
            if (rb30Dni == null || rbWszystkie == null) return;
            WczytajRanking(rbWszystkie.IsChecked == true);
        }

        private void ListaRanking_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (listaRanking.SelectedItem is DataRowView row)
            {
                bool wszystkie = rbWszystkie?.IsChecked == true;
                var okno = new HistoriaHandlowcaWindow(connectionString, row, wszystkie);
                okno.Show();
            }
        }

        private void WypelnijFiltryDynamiczne()
        {
            if (dtKontakty == null) return;
            var woj = dtKontakty.AsEnumerable().Select(r => r["Wojewodztwo"].ToString()).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x);
            foreach (var w in woj) cmbWojewodztwo.Items.Add(new ComboBoxItem { Content = w });
            var branze = dtKontakty.AsEnumerable().Select(r => r["PKD_Opis"].ToString()).Where(x => !string.IsNullOrEmpty(x)).Distinct().OrderBy(x => x);
            foreach (var b in branze) cmbBranza.Items.Add(new ComboBoxItem { Content = b });
        }
        #endregion

        #region Interakcje
        private void DgKontakty_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Anuluj edycję notatki przy zmianie wiersza
            if (edytowanaNotatkaId > 0)
            {
                edytowanaNotatkaId = 0;
                txtNowaNotatka.Text = "";
                btnDodajNotatke.Content = "Dodaj";
            }

            if (dgKontakty.SelectedItem is DataRowView row)
            {
                aktualnyOdbiorcaID = Convert.ToInt32(row["ID"]);

                // HEADER
                if (txtHeaderKlient != null) txtHeaderKlient.Text = row["NAZWA"].ToString();
                if (txtHeaderTelefon != null) txtHeaderTelefon.Text = row["TELEFON_K"].ToString();
                if (txtHeaderMiasto != null) txtHeaderMiasto.Text = row["MIASTO"].ToString();

                // PLANOWANY KONTAKT W HEADERZE
                if (row["DataNastepnegoKontaktu"] != DBNull.Value && txtKlientNastepnyKontakt != null)
                {
                    DateTime data = (DateTime)row["DataNastepnegoKontaktu"];
                    txtKlientNastepnyKontakt.Text = data.ToString("dd.MM");
                    if (panelNastepnyKontakt != null)
                    {
                        if (data < DateTime.Today) panelNastepnyKontakt.Background = (Brush)new BrushConverter().ConvertFrom("#FEE2E2");
                        else if (data == DateTime.Today) panelNastepnyKontakt.Background = (Brush)new BrushConverter().ConvertFrom("#DBEAFE");
                        else panelNastepnyKontakt.Background = (Brush)new BrushConverter().ConvertFrom("#DCFCE7");
                    }
                }
                else if (txtKlientNastepnyKontakt != null)
                {
                    txtKlientNastepnyKontakt.Text = "Brak";
                    if (panelNastepnyKontakt != null) panelNastepnyKontakt.Background = (Brush)new BrushConverter().ConvertFrom("#F3F4F6");
                }
                WczytajNotatki(aktualnyOdbiorcaID);
            }
        }

        private void WczytajNotatki(int id)
        {
            if (listaNotatek == null) return;
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // Łączymy notatki i zmiany statusów
                var cmd = new SqlCommand(@"
                    SELECT Id, Tresc, DataUtworzenia, Operator, Typ, CzyNotatka FROM (
                        SELECT n.ID as Id, n.Tresc, n.DataUtworzenia, ISNULL(o.Name, n.KtoDodal) as Operator,
                               '📝' as Typ, CAST(1 AS BIT) as CzyNotatka
                        FROM NotatkiCRM n
                        LEFT JOIN operators o ON n.KtoDodal = CAST(o.ID AS NVARCHAR)
                        WHERE n.IDOdbiorcy = @id
                        UNION ALL
                        SELECT 0 as Id, CONCAT('Status: ', h.WartoscNowa) as Tresc, h.DataZmiany as DataUtworzenia,
                               ISNULL(o.Name, h.KtoWykonal) as Operator, '🔄' as Typ, CAST(0 AS BIT) as CzyNotatka
                        FROM HistoriaZmianCRM h
                        LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                        WHERE h.IDOdbiorcy = @id AND h.TypZmiany = 'Zmiana statusu'
                    ) AS Historia
                    ORDER BY DataUtworzenia DESC", conn);
                cmd.Parameters.AddWithValue("@id", id);
                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable(); adapter.Fill(dt);
                var list = new ObservableCollection<NotatkaCRM>();
                foreach (DataRow r in dt.Rows) list.Add(new NotatkaCRM {
                    Id = Convert.ToInt32(r["Id"]),
                    Tresc = r["Tresc"].ToString(),
                    DataUtworzenia = (DateTime)r["DataUtworzenia"],
                    Operator = r["Operator"].ToString(),
                    Typ = r["Typ"].ToString(),
                    CzyNotatka = Convert.ToBoolean(r["CzyNotatka"])
                });
                listaNotatek.ItemsSource = list;
            }
        }

        private void BtnDodajNotatke_Click(object sender, RoutedEventArgs e)
        {
            if (aktualnyOdbiorcaID == 0 || string.IsNullOrWhiteSpace(txtNowaNotatka.Text)) return;

            // Tryb edycji
            if (edytowanaNotatkaId > 0)
            {
                ZapiszEdycjeNotatki(edytowanaNotatkaId, txtNowaNotatka.Text);
                edytowanaNotatkaId = 0;
                txtNowaNotatka.Text = "";
                btnDodajNotatke.Content = "Dodaj";
                return;
            }

            // Tryb dodawania
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal) VALUES (@id, @tresc, @op)", conn);
                    cmd.Parameters.AddWithValue("@id", aktualnyOdbiorcaID);
                    cmd.Parameters.AddWithValue("@tresc", txtNowaNotatka.Text);
                    cmd.Parameters.AddWithValue("@op", operatorID);
                    cmd.ExecuteNonQuery();
                }
                txtNowaNotatka.Text = "";
                WczytajNotatki(aktualnyOdbiorcaID);
                ShowToast("Notatka dodana! 📝");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private int edytowanaNotatkaId = 0;

        private void BtnEdytujNotatke_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is NotatkaCRM notatka)
            {
                edytowanaNotatkaId = notatka.Id;
                txtNowaNotatka.Text = notatka.Tresc;
                txtNowaNotatka.Focus();
                btnDodajNotatke.Content = "Zapisz";
            }
        }

        private void ZapiszEdycjeNotatki(int id, string nowyTekst)
        {
            try
            {
                // Dodaj znacznik "(edytowano)" jeśli jeszcze nie ma
                string tekstDoZapisu = nowyTekst.TrimEnd();
                if (!tekstDoZapisu.EndsWith("(edytowano)"))
                    tekstDoZapisu += " (edytowano)";

                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("UPDATE NotatkiCRM SET Tresc = @tresc WHERE ID = @id", conn);
                    cmd.Parameters.AddWithValue("@tresc", tekstDoZapisu);
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.ExecuteNonQuery();
                }
                WczytajNotatki(aktualnyOdbiorcaID);
                ShowToast("Notatka zaktualizowana! ✏️");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void BtnUsunNotatke_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is NotatkaCRM notatka)
            {
                var result = MessageBox.Show("Czy na pewno chcesz usunąć tę notatkę?", "Potwierdzenie",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            var cmd = new SqlCommand("DELETE FROM NotatkiCRM WHERE ID = @id", conn);
                            cmd.Parameters.AddWithValue("@id", notatka.Id);
                            cmd.ExecuteNonQuery();
                        }
                        WczytajNotatki(aktualnyOdbiorcaID);
                        ShowToast("Notatka usunięta! 🗑️");
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message); }
                }
            }
        }

        private void MenuTag_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView row && sender is MenuItem mi)
            {
                string tag = mi.Tag.ToString();
                string noweTagi = tag == "CLEAR" ? "" : tag;
                int id = (int)row["ID"];
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    new SqlCommand($"UPDATE OdbiorcyCRM SET Tagi = '{noweTagi}' WHERE ID={id}", conn).ExecuteNonQuery();
                }
                WczytajDane();
                ShowToast(tag == "CLEAR" ? "Usunięto tagi" : $"Oznaczono jako: {tag}");
            }
        }

        private async void ShowToast(string message)
        {
            if (toastPopup == null || toastText == null) return;
            toastText.Text = message;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            toastPopup.BeginAnimation(OpacityProperty, fadeIn);
            await Task.Delay(3000);
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
            toastPopup.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => Filtruj();
        private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e) => Filtruj();
        private void CmbWojewodztwo_SelectionChanged(object sender, SelectionChangedEventArgs e) => Filtruj();
        private void CmbBranza_SelectionChanged(object sender, SelectionChangedEventArgs e) => Filtruj();

        private void Filtruj()
        {
            if (dtKontakty == null) return;
            string filter = "1=1";
            string txt = txtSzukaj.Text.Trim();
            if (!string.IsNullOrEmpty(txt))
            {
                txt = txt.Replace("'", "''");
                filter += $" AND (NAZWA LIKE '%{txt}%' OR MIASTO LIKE '%{txt}%' OR TELEFON_K LIKE '%{txt}%')";
            }
            if (cmbStatus.SelectedIndex > 0) filter += $" AND Status = '{(cmbStatus.SelectedItem as ComboBoxItem).Content}'";
            if (cmbWojewodztwo.SelectedIndex > 0) filter += $" AND Wojewodztwo = '{(cmbWojewodztwo.SelectedItem as ComboBoxItem).Content}'";
            if (cmbBranza.SelectedIndex > 0) filter += $" AND PKD_Opis = '{(cmbBranza.SelectedItem as ComboBoxItem).Content}'";

            // Quick filter chips
            if (!string.IsNullOrEmpty(aktywnyChip))
            {
                if (aktywnyChip == "Zaległe")
                    filter += " AND DataNastepnegoKontaktu < #" + DateTime.Today.ToString("yyyy-MM-dd") + "#";
                else
                    filter += $" AND Tagi LIKE '%{aktywnyChip}%'";
            }

            dtKontakty.DefaultView.RowFilter = filter;
            if (txtLiczbaWynikow != null) txtLiczbaWynikow.Text = $"{dtKontakty.DefaultView.Count} klientów";
        }

        private void BtnKanban_Click(object sender, RoutedEventArgs e) { new KanbanWindow(connectionString, operatorID).Show(); }
        private void BtnDashboard_Click(object sender, RoutedEventArgs e) { new DashboardCRMWindow(connectionString).Show(); }
        private void BtnManager_Click(object sender, RoutedEventArgs e) { new PanelManageraWindow(connectionString).Show(); }
        private void BtnMapa_Click(object sender, RoutedEventArgs e) { new MapaCRMWindow(connectionString, operatorID).Show(); }
        private void BtnDodaj_Click(object sender, RoutedEventArgs e)
        {
            var okno = new OfertaCenowa.DodajOdbiorceWindow(connectionString, operatorID);
            if (okno.ShowDialog() == true)
            {
                // Jeśli użytkownik wybrał "Tylko moi", włącz ten filtr
                if (okno.FiltrujTylkoMoje && chkTylkoMoje != null)
                {
                    chkTylkoMoje.IsChecked = true;
                }
                WczytajDane();
            }
        }
        private void BtnOdswiez_Click(object sender, RoutedEventArgs e) => WczytajDane();
        private void ChkTylkoMoje_Changed(object sender, RoutedEventArgs e) => WczytajDane();
        private void BtnAdmin_Click(object sender, RoutedEventArgs e)
        {
            new CallReminderAdminPanel().Show();
        }

        private void BtnImportFirm_Click(object sender, RoutedEventArgs e)
        {
            var importDialog = new Dialogs.ImportFirmDialog(connectionString, operatorID);
            if (importDialog.ShowDialog() == true)
            {
                WczytajDane();
            }
        }


        private string aktywnyChip = "";

        private void Chip_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border chip && chip.Tag != null)
            {
                string tag = chip.Tag.ToString();
                if (aktywnyChip == tag)
                {
                    // Odznacz chip
                    aktywnyChip = "";
                    chip.BorderThickness = new Thickness(0);
                    chip.BorderBrush = null;
                }
                else
                {
                    // Zresetuj poprzedni chip
                    ResetujChipsy();
                    aktywnyChip = tag;
                    chip.BorderThickness = new Thickness(2);
                    chip.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
                }
                Filtruj();
            }
        }

        private void ChipZalegle_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border chip)
            {
                if (aktywnyChip == "Zaległe")
                {
                    aktywnyChip = "";
                    chip.BorderThickness = new Thickness(0);
                }
                else
                {
                    ResetujChipsy();
                    aktywnyChip = "Zaległe";
                    chip.BorderThickness = new Thickness(2);
                    chip.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
                }
                Filtruj();
            }
        }

        private void ResetujChipsy()
        {
            chipVIP.BorderThickness = new Thickness(0);
            chipPilne.BorderThickness = new Thickness(0);
            chipPremium.BorderThickness = new Thickness(0);
            chipZalegle.BorderThickness = new Thickness(0);
        }

        private void BtnKlientZadzwon_Click(object sender, RoutedEventArgs e)
        {
            if (txtHeaderTelefon == null) return;
            string tel = txtHeaderTelefon.Text.Replace(" ", "").Replace("-", "");
            if (tel.Length > 0 && tel != "-") Process.Start(new ProcessStartInfo($"tel:{tel}") { UseShellExecute = true });
        }

        private void BtnKlientEdytuj_Click(object sender, RoutedEventArgs e)
        {
            if (aktualnyOdbiorcaID == 0) return;
            if (new EdycjaKontaktuWindow { KlientID = aktualnyOdbiorcaID, OperatorID = operatorID }.ShowDialog() == true) WczytajDane();
        }

        private void TxtKlientTelefon_Click(object sender, MouseButtonEventArgs e) => BtnKlientZadzwon_Click(sender, null);
        private void DgKontakty_MouseDoubleClick(object sender, MouseButtonEventArgs e) => BtnKlientEdytuj_Click(sender, null);
        private void MenuZadzwon_Click(object sender, RoutedEventArgs e) => BtnKlientZadzwon_Click(sender, null);
        private void MenuEdytuj_Click(object sender, RoutedEventArgs e) => BtnKlientEdytuj_Click(sender, null);

        private void MenuUstawDate_Click(object sender, RoutedEventArgs e)
        {
            if (aktualnyOdbiorcaID == 0 && dgKontakty.SelectedItem is DataRowView row) aktualnyOdbiorcaID = (int)row["ID"];
            if (aktualnyOdbiorcaID == 0) return;
            var dialog = new UstawDateKontaktuDialog(txtHeaderKlient.Text);
            if (dialog.ShowDialog() == true)
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    new SqlCommand($"UPDATE OdbiorcyCRM SET DataNastepnegoKontaktu = '{dialog.WybranaData:yyyy-MM-dd}' WHERE ID={aktualnyOdbiorcaID}", conn).ExecuteNonQuery();
                }
                WczytajDane();
            }
        }

        private void MenuStatus_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is MenuItem mi && mi.Tag != null)
            {
                string nowyStatus = mi.Tag.ToString();
                int id = 0;
                if (dgKontakty.SelectedItem is DataRowView row) id = (int)row["ID"];
                if (id > 0)
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        new SqlCommand($"UPDATE OdbiorcyCRM SET Status = '{nowyStatus}' WHERE ID={id}", conn).ExecuteNonQuery();
                        var cmdLog = new SqlCommand("INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany) VALUES (@id, 'Zmiana statusu', @val, @op, GETDATE())", conn);
                        cmdLog.Parameters.AddWithValue("@id", id);
                        cmdLog.Parameters.AddWithValue("@val", nowyStatus);
                        cmdLog.Parameters.AddWithValue("@op", operatorID);
                        cmdLog.ExecuteNonQuery();
                    }
                    WczytajDane();
                    ShowToast("Zmieniono status na: " + nowyStatus);
                }
            }
        }

        private void MenuZmienStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag != null)
            {
                string nowyStatus = mi.Tag.ToString();
                int id = 0;
                if (dgKontakty.SelectedItem is DataRowView row) id = (int)row["ID"];
                if (id > 0)
                {
                    using (var conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        var cmdUpdate = new SqlCommand("UPDATE OdbiorcyCRM SET Status = @status WHERE ID = @id", conn);
                        cmdUpdate.Parameters.AddWithValue("@status", nowyStatus);
                        cmdUpdate.Parameters.AddWithValue("@id", id);
                        cmdUpdate.ExecuteNonQuery();

                        var cmdLog = new SqlCommand("INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany) VALUES (@id, 'Zmiana statusu', @val, @op, GETDATE())", conn);
                        cmdLog.Parameters.AddWithValue("@id", id);
                        cmdLog.Parameters.AddWithValue("@val", nowyStatus);
                        cmdLog.Parameters.AddWithValue("@op", operatorID);
                        cmdLog.ExecuteNonQuery();
                    }
                    WczytajDane();
                    ShowToast($"Status zmieniony na: {nowyStatus}");
                }
            }
        }

        private void MenuGoogle_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView row)
            {
                string nazwa = row["NAZWA"].ToString();
                string miasto = row["MIASTO"].ToString();
                string query = System.Net.WebUtility.UrlEncode($"{nazwa} {miasto}");
                Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={query}") { UseShellExecute = true });
            }
        }

        private void MenuTrasa_Click(object sender, RoutedEventArgs e)
        {
            if (dgKontakty.SelectedItem is DataRowView row)
            {
                string adres = $"{row["ULICA"]}, {row["KOD"]} {row["MIASTO"]}";
                string query = System.Net.WebUtility.UrlEncode(adres);
                // Koziołki 40, 95-061 Dmosin jako punkt startowy (baza firmy)
                string origin = System.Net.WebUtility.UrlEncode("Koziołki 40, 95-061 Dmosin");
                Process.Start(new ProcessStartInfo($"https://www.google.com/maps/dir/{origin}/{query}") { UseShellExecute = true });
            }
        }
        #endregion
    }

    public class NotatkaCRM
    {
        public int Id { get; set; }
        public string Tresc { get; set; }
        public DateTime DataUtworzenia { get; set; }
        public string Operator { get; set; }
        public string Typ { get; set; } = "📝";
        public bool CzyNotatka { get; set; } = true;
    }
}