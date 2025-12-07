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

        private void WczytajKontakty()
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                // Dodajemy NIP (jeśli istnieje) lub pomijamy
                // Bezpieczne zapytanie z kolumnami zdefiniowanymi w DataGrid
                var cmd = new SqlCommand(@"
                    SELECT o.ID, o.Nazwa as NAZWA, o.KOD, o.MIASTO, o.ULICA, o.Telefon_K as TELEFON_K, o.Email, 
                        o.Wojewodztwo, o.PKD_Opis, o.Tagi, ISNULL(o.Status, 'Do zadzwonienia') as Status, o.DataNastepnegoKontaktu,
                        (SELECT TOP 1 DataZmiany FROM HistoriaZmianCRM WHERE IDOdbiorcy = o.ID ORDER BY DataZmiany DESC) as OstatniaZmiana
                    FROM OdbiorcyCRM o LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
                    WHERE (w.OperatorID = @OperatorID OR w.OperatorID IS NULL) AND ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')
                    ORDER BY CASE WHEN o.DataNastepnegoKontaktu IS NULL THEN 1 ELSE 0 END, o.DataNastepnegoKontaktu ASC", conn);

                cmd.Parameters.AddWithValue("@OperatorID", operatorID);
                var adapter = new SqlDataAdapter(cmd);
                dtKontakty = new DataTable();
                adapter.Fill(dtKontakty);

                if (!dtKontakty.Columns.Contains("CzyZaniedbany")) dtKontakty.Columns.Add("CzyZaniedbany", typeof(bool));
                if (!dtKontakty.Columns.Contains("MaTagi")) dtKontakty.Columns.Add("MaTagi", typeof(bool));

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
                }
                dgKontakty.ItemsSource = dtKontakty.DefaultView;
                if (txtLiczbaWynikow != null) txtLiczbaWynikow.Text = $"{dtKontakty.Rows.Count} klientów";
            }
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

        private void WczytajRanking()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand(@"
                        SELECT TOP 10 ROW_NUMBER() OVER (ORDER BY COUNT(*) DESC) as Pozycja,
                            ISNULL(o.Name, 'ID: ' + h.KtoWykonal) as Operator,
                            COUNT(*) as Suma,
                            SUM(CASE WHEN WartoscNowa = 'Do zadzwonienia' OR WartoscNowa = 'Nowy' THEN 1 ELSE 0 END) as DoZadzwonienia,
                            SUM(CASE WHEN WartoscNowa = 'Próba kontaktu' THEN 1 ELSE 0 END) as Proby,
                            SUM(CASE WHEN WartoscNowa = 'Nawiązano kontakt' THEN 1 ELSE 0 END) as Nawiazano,
                            SUM(CASE WHEN WartoscNowa = 'Zgoda na dalszy kontakt' THEN 1 ELSE 0 END) as Zgoda,
                            SUM(CASE WHEN WartoscNowa = 'Do wysłania oferta' THEN 1 ELSE 0 END) as Oferty,
                            SUM(CASE WHEN WartoscNowa = 'Nie zainteresowany' THEN 1 ELSE 0 END) as NieZainteresowany
                        FROM HistoriaZmianCRM h LEFT JOIN operators o ON h.KtoWykonal = CAST(o.ID AS NVARCHAR)
                        WHERE h.DataZmiany > DATEADD(day, -30, GETDATE()) AND h.TypZmiany IS NOT NULL
                        GROUP BY h.KtoWykonal, o.Name ORDER BY Suma DESC", conn);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    if (listaRanking != null) listaRanking.ItemsSource = dt.DefaultView;
                }
            }
            catch { }
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
            if (dgKontakty.SelectedItem is DataRowView row)
            {
                aktualnyOdbiorcaID = Convert.ToInt32(row["ID"]);

                // HEADER
                if (txtHeaderKlient != null) txtHeaderKlient.Text = row["NAZWA"].ToString();
                if (txtHeaderTelefon != null) txtHeaderTelefon.Text = row["TELEFON_K"].ToString();
                if (txtHeaderMiasto != null) txtHeaderMiasto.Text = row["MIASTO"].ToString();

                // SZCZEGÓŁY - TERAZ BEZPIECZNIE
                if (txtSzczegolyAdres != null) txtSzczegolyAdres.Text = $"{row["ULICA"]}, {row["MIASTO"]}";
                if (txtSzczegolyEmail != null) txtSzczegolyEmail.Text = row["Email"].ToString();
                if (txtSzczegolyBranza != null) txtSzczegolyBranza.Text = row["PKD_Opis"].ToString();

                // PLANOWANY KONTAKT
                if (row["DataNastepnegoKontaktu"] != DBNull.Value && txtKlientNastepnyKontakt != null)
                {
                    DateTime data = (DateTime)row["DataNastepnegoKontaktu"];
                    txtKlientNastepnyKontakt.Text = data.ToString("dd.MM.yyyy (dddd)");
                    if (panelNastepnyKontakt != null)
                    {
                        if (data < DateTime.Today) panelNastepnyKontakt.Background = (Brush)new BrushConverter().ConvertFrom("#FEE2E2");
                        else if (data == DateTime.Today) panelNastepnyKontakt.Background = (Brush)new BrushConverter().ConvertFrom("#DBEAFE");
                        else panelNastepnyKontakt.Background = (Brush)new BrushConverter().ConvertFrom("#F3F4F6");
                    }
                }
                else if (txtKlientNastepnyKontakt != null)
                {
                    txtKlientNastepnyKontakt.Text = "Brak zaplanowanego kontaktu";
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
                var cmd = new SqlCommand("SELECT Tresc, DataUtworzenia, KtoDodal as Operator FROM NotatkiCRM WHERE IDOdbiorcy = @id ORDER BY DataUtworzenia DESC", conn);
                cmd.Parameters.AddWithValue("@id", id);
                var adapter = new SqlDataAdapter(cmd);
                var dt = new DataTable(); adapter.Fill(dt);
                var list = new ObservableCollection<NotatkaCRM>();
                foreach (DataRow r in dt.Rows) list.Add(new NotatkaCRM { Tresc = r["Tresc"].ToString(), DataUtworzenia = (DateTime)r["DataUtworzenia"], Operator = r["Operator"].ToString() });
                listaNotatek.ItemsSource = list;
            }
        }

        private void BtnDodajNotatke_Click(object sender, RoutedEventArgs e)
        {
            if (aktualnyOdbiorcaID == 0 || string.IsNullOrWhiteSpace(txtNowaNotatka.Text)) return;
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
            dtKontakty.DefaultView.RowFilter = filter;
            if (txtLiczbaWynikow != null) txtLiczbaWynikow.Text = $"{dtKontakty.DefaultView.Count} klientów";
        }

        private void BtnKanban_Click(object sender, RoutedEventArgs e) { new KanbanWindow(connectionString, operatorID).Show(); }
        private void BtnManager_Click(object sender, RoutedEventArgs e) { new PanelManageraWindow(connectionString).Show(); }
        private void BtnMapa_Click(object sender, RoutedEventArgs e) { new MapaCRMWindow(connectionString, operatorID).Show(); }
        private void BtnDodaj_Click(object sender, RoutedEventArgs e) { if (new FormDodajOdbiorce(connectionString, operatorID).ShowDialog() == System.Windows.Forms.DialogResult.OK) WczytajDane(); }
        private void BtnOdswiez_Click(object sender, RoutedEventArgs e) => WczytajDane();
        private void BtnAdmin_Click(object sender, RoutedEventArgs e) { }

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
        #endregion
    }

    public class NotatkaCRM
    {
        public string Tresc { get; set; }
        public DateTime DataUtworzenia { get; set; }
        public string Operator { get; set; }
    }
}