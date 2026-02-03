using Kalendarz1.OfertaCenowa;
using Kalendarz1.CRM.Services;
using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Kalendarz1.CRM
{
    public partial class CRMWindow : Window
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";
        private string operatorID = "";
        private int aktualnyOdbiorcaID = 0;
        private DataTable dtKontakty;
        private bool isLoading = false;

        // Avatar cache: operator name → BitmapSource
        private readonly Dictionary<string, BitmapSource> _handlowiecAvatarCache = new();
        private readonly Dictionary<string, string> _handlowiecNameToId = new();

        public string UserID { get; set; }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public CRMWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            Loaded += CRMWindow_Loaded;
            dgKontakty.LoadingRow += DgKontakty_LoadingRow;
        }

        private void CRMWindow_Loaded(object sender, RoutedEventArgs e)
        {
            operatorID = UserID;
            if (operatorID != "11111")
            {
                if (btnManager != null) btnManager.Visibility = Visibility.Collapsed;
                if (btnAdmin != null) btnAdmin.Visibility = Visibility.Collapsed;
            }

            // Load and apply saved theme
            CRMThemeService.Load();
            if (CRMThemeService.CurrentTheme == CRMThemeMode.Light)
                ApplyCRMTheme(true);
            else
                UpdateThemeButton(false);

            // Załaduj avatar i nazwę zalogowanego użytkownika
            LoadCurrentUserInfo();

            InicjalizujFiltry();
            LoadHandlowiecAvatarMap();
            WczytajDane();
        }

        private void LoadCurrentUserInfo()
        {
            try
            {
                // Pobierz nazwę użytkownika
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT Name FROM operators WHERE ID = @id", conn);
                    cmd.Parameters.AddWithValue("@id", operatorID);
                    var name = cmd.ExecuteScalar()?.ToString();
                    if (!string.IsNullOrEmpty(name) && txtUserName != null)
                        txtUserName.Text = name;
                }

                // Załaduj avatar
                if (imgUserAvatar != null)
                {
                    System.Drawing.Image avatarImg = null;
                    if (UserAvatarManager.HasAvatar(operatorID))
                        avatarImg = UserAvatarManager.GetAvatarRounded(operatorID, 28);

                    if (avatarImg == null)
                    {
                        string userName = txtUserName?.Text ?? "U";
                        avatarImg = UserAvatarManager.GenerateDefaultAvatar(userName, operatorID, 28);
                    }

                    if (avatarImg != null)
                    {
                        using (avatarImg)
                        using (var bmp = new System.Drawing.Bitmap(avatarImg))
                        {
                            var hBitmap = bmp.GetHbitmap();
                            try
                            {
                                var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                    hBitmap, IntPtr.Zero, Int32Rect.Empty,
                                    BitmapSizeOptions.FromEmptyOptions());
                                source.Freeze();
                                imgUserAvatar.Source = source;
                            }
                            finally { DeleteObject(hBitmap); }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadCurrentUserInfo error: {ex.Message}");
            }
        }

        #region Handlowiec Avatars

        private void LoadHandlowiecAvatarMap()
        {
            try
            {
                using var conn = new SqlConnection(connectionString);
                conn.Open();
                using var cmd = new SqlCommand("SELECT ID, Name FROM operators WHERE Name IS NOT NULL", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string id = reader["ID"].ToString();
                    string name = reader["Name"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(name) && !_handlowiecNameToId.ContainsKey(name))
                        _handlowiecNameToId[name] = id;
                }
            }
            catch { }
        }

        private BitmapSource GetHandlowiecAvatar(string name, string directId = null)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            // Use directId as cache key when available for precise matching
            string cacheKey = !string.IsNullOrEmpty(directId) ? $"id:{directId}" : name;
            if (_handlowiecAvatarCache.TryGetValue(cacheKey, out var cached))
                return cached;

            BitmapSource source = null;
            try
            {
                string userId = !string.IsNullOrEmpty(directId) ? directId
                    : _handlowiecNameToId.TryGetValue(name, out var id) ? id : name;

                System.Drawing.Image img = null;
                if (UserAvatarManager.HasAvatar(userId))
                    img = UserAvatarManager.GetAvatarRounded(userId, 28);
                if (img == null)
                    img = UserAvatarManager.GenerateDefaultAvatar(name, userId, 28);

                if (img != null)
                {
                    using (img)
                    using (var bmp = new System.Drawing.Bitmap(img))
                    {
                        var hBitmap = bmp.GetHbitmap();
                        try
                        {
                            source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                                BitmapSizeOptions.FromEmptyOptions());
                            source.Freeze();
                        }
                        finally { DeleteObject(hBitmap); }
                    }
                }
            }
            catch { }

            _handlowiecAvatarCache[cacheKey] = source;
            return source;
        }

        private void DgKontakty_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView drv)
            {
                string handlowiec = drv["OstatniHandlowiec"]?.ToString();
                if (string.IsNullOrWhiteSpace(handlowiec)) return;

                // Use the direct operator ID for precise avatar matching
                string handlowiecId = drv.Row.Table.Columns.Contains("OstatniHandlowiecID")
                    ? drv["OstatniHandlowiecID"]?.ToString() : null;

                // Defer avatar loading to after layout
                e.Row.Loaded += (s, args) =>
                {
                    try
                    {
                        var row = s as DataGridRow;
                        if (row == null) return;

                        // Find the Image element inside the Handlowiec cell
                        var presenter = FindVisualChild<DataGridCellsPresenter>(row);
                        if (presenter == null) return;

                        // Handlowiec is column index 2 (Status=0, Firma=1, Handlowiec=2)
                        var cell = presenter.ItemContainerGenerator.ContainerFromIndex(2) as DataGridCell;
                        if (cell == null) return;

                        var img = FindVisualChild<System.Windows.Controls.Image>(cell);
                        if (img != null)
                        {
                            var avatarSource = GetHandlowiecAvatar(handlowiec, handlowiecId);
                            if (avatarSource != null)
                                img.Source = avatarSource;
                        }
                    }
                    catch { }
                };
            }
        }

        private void LoadRankingAvatars()
        {
            try
            {
                if (listaRanking == null || listaRanking.Items.Count == 0) return;

                for (int i = 0; i < listaRanking.Items.Count; i++)
                {
                    var container = listaRanking.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (container == null) continue;

                    var img = FindVisualChild<System.Windows.Controls.Image>(container);
                    if (img == null || img.Source != null) continue;

                    if (listaRanking.Items[i] is DataRowView drv)
                    {
                        string operatorName = drv["Operator"]?.ToString();
                        string operatorId = drv["OperatorID"]?.ToString();
                        if (string.IsNullOrWhiteSpace(operatorName)) continue;

                        BitmapSource avatar = null;
                        try
                        {
                            string userId = !string.IsNullOrEmpty(operatorId) ? operatorId : operatorName;
                            System.Drawing.Image avatarImg = null;
                            if (UserAvatarManager.HasAvatar(userId))
                                avatarImg = UserAvatarManager.GetAvatarRounded(userId, 30);
                            if (avatarImg == null)
                                avatarImg = UserAvatarManager.GenerateDefaultAvatar(operatorName, userId, 30);

                            if (avatarImg != null)
                            {
                                using (avatarImg)
                                using (var bmp = new System.Drawing.Bitmap(avatarImg))
                                {
                                    var hBitmap = bmp.GetHbitmap();
                                    try
                                    {
                                        avatar = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                                            hBitmap, IntPtr.Zero, Int32Rect.Empty,
                                            BitmapSizeOptions.FromEmptyOptions());
                                        avatar.Freeze();
                                    }
                                    finally { DeleteObject(hBitmap); }
                                }
                            }
                        }
                        catch { }

                        if (avatar != null)
                            img.Source = avatar;
                    }
                }
            }
            catch { }
        }

        private void LoadNotatkiAvatars()
        {
            try
            {
                if (listaNotatek == null || listaNotatek.Items.Count == 0) return;

                for (int i = 0; i < listaNotatek.Items.Count; i++)
                {
                    var container = listaNotatek.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                    if (container == null) continue;

                    var img = FindVisualChild<System.Windows.Controls.Image>(container);
                    if (img == null || img.Source != null) continue;

                    if (listaNotatek.Items[i] is NotatkaCRM notatka)
                    {
                        string operatorName = notatka.Operator;
                        if (string.IsNullOrWhiteSpace(operatorName)) continue;

                        var avatar = GetHandlowiecAvatar(operatorName);
                        if (avatar != null)
                            img.Source = avatar;
                    }
                }
            }
            catch { }
        }

        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T t) return t;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        #endregion

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
            _handlowiecAvatarCache.Clear();
            bool tylkoMoje = chkTylkoMoje?.IsChecked == true;

            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();

                // Upewnij się że tabela WlascicieleOdbiorcow ma kolumnę Priorytet
                using (var cmdCheck = new SqlCommand(@"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('WlascicieleOdbiorcow') AND name = 'Priorytet')
                    ALTER TABLE WlascicieleOdbiorcow ADD Priorytet BIT DEFAULT 0", conn))
                {
                    cmdCheck.ExecuteNonQuery();
                }

                // Różne zapytanie w zależności od filtra "Tylko moi"
                string whereClause = tylkoMoje
                    ? "WHERE w.OperatorID = @OperatorID AND ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')"
                    : "WHERE (w.OperatorID = @OperatorID OR w.OperatorID IS NULL) AND ISNULL(o.Status, '') NOT IN ('Poprosił o usunięcie', 'Błędny rekord (do raportu)')";

                var cmd = new SqlCommand($@"
                    SELECT o.ID, o.Nazwa as NAZWA, o.KOD, o.MIASTO, o.ULICA, o.Telefon_K as TELEFON_K, o.Email,
                        o.Wojewodztwo, o.PKD_Opis, o.Tagi, ISNULL(o.Status, 'Do zadzwonienia') as Status, o.DataNastepnegoKontaktu,
                        (SELECT TOP 1 Data FROM (
                            SELECT DataZmiany as Data FROM HistoriaZmianCRM WHERE IDOdbiorcy = o.ID
                            UNION ALL
                            SELECT DataUtworzenia FROM NotatkiCRM WHERE IDOdbiorcy = o.ID
                        ) daty ORDER BY Data DESC) as OstatniaZmiana,
                        (SELECT TOP 1 ISNULL(op.Name, x.Operator) FROM (
                            SELECT CAST(KtoDodal AS VARCHAR(15)) as Operator, DataUtworzenia as Data FROM NotatkiCRM WHERE IDOdbiorcy = o.ID
                            UNION ALL
                            SELECT KtoWykonal, DataZmiany FROM HistoriaZmianCRM WHERE IDOdbiorcy = o.ID
                        ) x LEFT JOIN operators op ON op.ID = x.Operator OR op.Name = x.Operator
                        ORDER BY x.Data DESC) as OstatniHandlowiec,
                        (SELECT TOP 1 COALESCE(op2.ID, x.Operator) FROM (
                            SELECT CAST(KtoDodal AS VARCHAR(15)) as Operator, DataUtworzenia as Data FROM NotatkiCRM WHERE IDOdbiorcy = o.ID
                            UNION ALL
                            SELECT KtoWykonal, DataZmiany FROM HistoriaZmianCRM WHERE IDOdbiorcy = o.ID
                        ) x LEFT JOIN operators op2 ON op2.ID = x.Operator OR op2.Name = x.Operator
                        ORDER BY x.Data DESC) as OstatniHandlowiecID,
                        kp.Latitude, kp.Longitude
                    FROM OdbiorcyCRM o
                    LEFT JOIN WlascicieleOdbiorcow w ON o.ID = w.IDOdbiorcy
                    LEFT JOIN KodyPocztowe kp ON o.KOD = kp.Kod
                    {whereClause}
                    ORDER BY
                        ISNULL(w.Priorytet, 0) DESC,
                        CASE WHEN o.DataNastepnegoKontaktu IS NULL THEN 1 ELSE 0 END,
                        o.DataNastepnegoKontaktu ASC", conn);

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
            // KPI cards removed from UI - method kept for compatibility
        }

        private void ObliczTargetDnia()
        {
            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Pobierz cel dnia z tabeli CallReminderSalesTargets
                    int CEL = 15;
                    var cmdTarget = new SqlCommand(@"
                        SELECT TOP 1 DailyTarget FROM CallReminderSalesTargets
                        WHERE UserID = @op", conn);
                    cmdTarget.Parameters.AddWithValue("@op", operatorID);
                    var targetResult = cmdTarget.ExecuteScalar();
                    if (targetResult != null && targetResult != DBNull.Value)
                        CEL = Convert.ToInt32(targetResult);

                    var cmd = new SqlCommand("SELECT COUNT(*) FROM HistoriaZmianCRM WHERE KtoWykonal = @op AND CAST(DataZmiany AS DATE) = CAST(GETDATE() AS DATE)", conn);
                    cmd.Parameters.AddWithValue("@op", operatorID);
                    int wykonane = (int)cmd.ExecuteScalar();

                    if (txtTargetLabel != null) txtTargetLabel.Text = $"CEL DNIA ({CEL})";
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

        private string FormatujTelefon(string telefon)
        {
            if (string.IsNullOrWhiteSpace(telefon)) return "";

            // Usuń wszystkie znaki niebędące cyframi
            var digits = new string(telefon.Where(char.IsDigit).ToArray());

            if (digits.Length == 9)
            {
                // Format: XXX XXX XXX
                return $"{digits.Substring(0, 3)} {digits.Substring(3, 3)} {digits.Substring(6, 3)}";
            }
            else if (digits.Length == 11 && digits.StartsWith("48"))
            {
                // Format: +48 XXX XXX XXX
                return $"+48 {digits.Substring(2, 3)} {digits.Substring(5, 3)} {digits.Substring(8, 3)}";
            }
            else if (digits.Length >= 7)
            {
                // Format ogólny z separatorami co 3 cyfry
                var result = "";
                for (int i = 0; i < digits.Length; i++)
                {
                    if (i > 0 && i % 3 == 0) result += " ";
                    result += digits[i];
                }
                return result;
            }

            return telefon;
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
                            h.KtoWykonal as OperatorID,
                            COUNT(*) as Suma,
                            SUM(CASE WHEN WartoscNowa = 'Próba kontaktu' THEN 1 ELSE 0 END) as Proby,
                            SUM(CASE WHEN WartoscNowa = 'Nawiązano kontakt' THEN 1 ELSE 0 END) as Nawiazano,
                            SUM(CASE WHEN WartoscNowa = 'Zgoda na dalszy kontakt' THEN 1 ELSE 0 END) as Zgoda,
                            SUM(CASE WHEN WartoscNowa = 'Do wysłania oferta' THEN 1 ELSE 0 END) as Oferty,
                            SUM(CASE WHEN WartoscNowa = 'Nie zainteresowany' THEN 1 ELSE 0 END) as NieZainteresowany
                        FROM HistoriaZmianCRM h LEFT JOIN operators o ON o.ID = h.KtoWykonal
                        {whereDate}
                        GROUP BY h.KtoWykonal, o.Name ORDER BY Suma DESC", conn);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);
                    if (listaRanking != null)
                    {
                        listaRanking.ItemsSource = dt.DefaultView;
                        // Load avatars into ranking items after layout
                        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => LoadRankingAvatars()));
                    }

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
                if (txtHeaderTelefon != null) txtHeaderTelefon.Text = FormatujTelefon(row["TELEFON_K"].ToString());
                if (txtHeaderMiasto != null) txtHeaderMiasto.Text = row["MIASTO"].ToString();

                // PANEL BOCZNY - AKTYWNY ODBIORCA
                if (txtPanelKlientNazwa != null) txtPanelKlientNazwa.Text = row["NAZWA"].ToString();
                if (txtPanelKlientMiasto != null) txtPanelKlientMiasto.Text = row["MIASTO"].ToString();
                if (txtPanelKlientTelefon != null) txtPanelKlientTelefon.Text = FormatujTelefon(row["TELEFON_K"].ToString());

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
                        LEFT JOIN operators o ON o.ID = CAST(n.KtoDodal AS VARCHAR(15))
                        WHERE n.IDOdbiorcy = @id
                        UNION ALL
                        SELECT 0 as Id, CONCAT('Status: ', h.WartoscNowa) as Tresc, h.DataZmiany as DataUtworzenia,
                               ISNULL(o.Name, h.KtoWykonal) as Operator, '🔄' as Typ, CAST(0 AS BIT) as CzyNotatka
                        FROM HistoriaZmianCRM h
                        LEFT JOIN operators o ON o.ID = h.KtoWykonal
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
                // Load avatars after layout
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => LoadNotatkiAvatars()));
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
        private void BtnReminder_Click(object sender, RoutedEventArgs e)
        {
            var config = new Models.CallReminderConfig { UserID = operatorID };
            new CallReminderWindow(connectionString, operatorID, config).Show();
        }
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

        #region Theme Switching

        private void BtnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            CRMThemeService.Toggle();
            bool isLight = CRMThemeService.CurrentTheme == CRMThemeMode.Light;
            ApplyCRMTheme(isLight);
        }

        private void UpdateThemeButton(bool isLight)
        {
            if (btnToggleTheme != null)
                btnToggleTheme.Content = isLight ? "\U0001F319 Ciemny" : "\u2600\uFE0F Jasny";
        }

        private void ApplyCRMTheme(bool isLight)
        {
            UpdateThemeButton(isLight);

            if (isLight)
            {
                // ══════════════════════════════════════════════
                // LIGHT THEME - profesjonalny jasny z kontrastem
                // ══════════════════════════════════════════════

                // Tło okna: ciepły szary (NIE biały) daje kontrast
                mainWindow.Background = new SolidColorBrush(Color.FromRgb(241, 245, 249));  // #F1F5F9 slate-100

                // ── Tła: wyraźna hierarchia jasności ──
                Resources["BgPrimary"] = new SolidColorBrush(Color.FromRgb(241, 245, 249));    // #F1F5F9 główne tło
                Resources["BgSecondary"] = new SolidColorBrush(Color.FromRgb(255, 255, 255));   // #FFFFFF karty/panele
                Resources["BgTertiary"] = new SolidColorBrush(Color.FromRgb(226, 232, 240));    // #E2E8F0 zagnieżdżone
                Resources["BgElevated"] = new SolidColorBrush(Color.FromRgb(203, 213, 225));    // #CBD5E1 wyróżnione

                // ── Akcent: ciemny zielony dobrze widoczny na jasnym ──
                Resources["AccentPrimary"] = new SolidColorBrush(Color.FromRgb(21, 128, 61));   // #15803D green-700
                Resources["AccentLight"] = new SolidColorBrush(Color.FromRgb(34, 197, 94));     // #22C55E green-500
                Resources["AccentBg"] = new SolidColorBrush(Color.FromRgb(220, 252, 231));      // #DCFCE7 green-100

                // ── Tekst: ciemny na jasnym - wysoki kontrast ──
                Resources["TextPrimary"] = new SolidColorBrush(Color.FromRgb(15, 23, 42));      // #0F172A slate-900
                Resources["TextSecondary"] = new SolidColorBrush(Color.FromRgb(51, 65, 85));    // #334155 slate-700
                Resources["TextMuted"] = new SolidColorBrush(Color.FromRgb(100, 116, 139));     // #64748B slate-500

                // ── Bordy: widoczne szare linie ──
                Resources["BorderDefault"] = new SolidColorBrush(Color.FromRgb(203, 213, 225)); // #CBD5E1 slate-300
                Resources["BorderLight"] = new SolidColorBrush(Color.FromRgb(226, 232, 240));   // #E2E8F0 slate-200
                Resources["BorderAccent"] = new SolidColorBrush(Color.FromRgb(21, 128, 61));    // #15803D

                // ── Aliasy kompatybilności ──
                Resources["PrimaryColor"] = new SolidColorBrush(Color.FromRgb(21, 128, 61));
                Resources["PrimaryLight"] = new SolidColorBrush(Color.FromRgb(220, 252, 231));
                Resources["TextDark"] = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                Resources["TextGray"] = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                Resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(203, 213, 225));

                // ── Statusy: jasne tła z ciemnymi tekstami ──
                Resources["StatusSuccessBg"] = new SolidColorBrush(Color.FromRgb(220, 252, 231));
                Resources["StatusWarningBg"] = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                Resources["StatusInfoBg"] = new SolidColorBrush(Color.FromRgb(219, 234, 254));
                Resources["StatusDangerBg"] = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                Resources["StatusNeutralBg"] = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                Resources["StatusTealBg"] = new SolidColorBrush(Color.FromRgb(204, 251, 241));

                // ── DataGrid: biały z wyraźnymi liniami ──
                dgKontakty.Background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                dgKontakty.RowBackground = new SolidColorBrush(Color.FromRgb(255, 255, 255));
                dgKontakty.AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(248, 250, 252)); // #F8FAFC
                dgKontakty.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
                dgKontakty.HorizontalGridLinesBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                dgKontakty.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                dgKontakty.BorderThickness = new Thickness(1);

                // Nagłówek tabeli: szary gradient, ciemny tekst
                var headerStyle = new Style(typeof(DataGridColumnHeader));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty,
                    new SolidColorBrush(Color.FromRgb(226, 232, 240))));  // #E2E8F0 widoczne tło
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty,
                    new SolidColorBrush(Color.FromRgb(30, 41, 59))));     // #1E293B ciemny tekst
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(12, 14, 12, 14)));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, 13.0));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 1, 2)));
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty,
                    new SolidColorBrush(Color.FromRgb(203, 213, 225)))); // #CBD5E1
                headerStyle.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
                dgKontakty.ColumnHeaderStyle = headerStyle;

                // Komórki
                var cellStyle = new Style(typeof(DataGridCell));
                cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
                cellStyle.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
                cellStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(8, 4, 8, 4)));
                cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
                cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty,
                    new SolidColorBrush(Color.FromRgb(15, 23, 42))));    // #0F172A
                dgKontakty.CellStyle = cellStyle;

                // Wiersze z hover i selekcją
                var rowStyle = new Style(typeof(DataGridRow));
                rowStyle.Setters.Add(new Setter(DataGridRow.HeightProperty, 60.0));
                rowStyle.Setters.Add(new Setter(DataGridRow.FontSizeProperty, 14.0));
                rowStyle.Setters.Add(new Setter(DataGridRow.BackgroundProperty, Brushes.White));
                rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
                rowStyle.Setters.Add(new Setter(DataGridRow.BorderBrushProperty,
                    new SolidColorBrush(Color.FromRgb(226, 232, 240))));  // #E2E8F0 visible line
                var hoverTrigger = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
                hoverTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty,
                    new SolidColorBrush(Color.FromRgb(219, 234, 254)))); // #DBEAFE blue-100 hover
                hoverTrigger.Setters.Add(new Setter(DataGridRow.BorderBrushProperty,
                    new SolidColorBrush(Color.FromRgb(147, 197, 253)))); // #93C5FD blue-300 border
                hoverTrigger.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(2)));
                rowStyle.Triggers.Add(hoverTrigger);
                var selectedTrigger = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
                selectedTrigger.Setters.Add(new Setter(DataGridRow.BackgroundProperty,
                    new SolidColorBrush(Color.FromRgb(191, 219, 254)))); // #BFDBFE blue-200 selected bg
                selectedTrigger.Setters.Add(new Setter(DataGridRow.ForegroundProperty,
                    new SolidColorBrush(Color.FromRgb(15, 23, 42))));    // dark text on blue
                selectedTrigger.Setters.Add(new Setter(DataGridRow.BorderBrushProperty,
                    new SolidColorBrush(Color.FromRgb(59, 130, 246))));  // #3B82F6 blue-500 border
                selectedTrigger.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(2)));
                rowStyle.Triggers.Add(selectedTrigger);
                dgKontakty.RowStyle = rowStyle;

                // Walk tree
                ApplyThemeToTree(this, true);
            }
            else
            {
                // ── DARK THEME: restore original ──
                mainWindow.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));

                Resources["BgPrimary"] = new SolidColorBrush(Color.FromRgb(15, 23, 42));
                Resources["BgSecondary"] = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                Resources["BgTertiary"] = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                Resources["BgElevated"] = new SolidColorBrush(Color.FromRgb(71, 85, 105));

                Resources["AccentPrimary"] = new SolidColorBrush(Color.FromRgb(99, 102, 241));
                Resources["AccentLight"] = new SolidColorBrush(Color.FromRgb(165, 180, 252));
                Resources["AccentBg"] = new SolidColorBrush(Color.FromRgb(49, 46, 129));

                Resources["TextPrimary"] = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                Resources["TextSecondary"] = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                Resources["TextMuted"] = new SolidColorBrush(Color.FromRgb(100, 116, 139));

                Resources["BorderDefault"] = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                Resources["BorderLight"] = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                Resources["BorderAccent"] = new SolidColorBrush(Color.FromRgb(99, 102, 241));

                Resources["PrimaryColor"] = new SolidColorBrush(Color.FromRgb(99, 102, 241));
                Resources["PrimaryLight"] = new SolidColorBrush(Color.FromRgb(49, 46, 129));
                Resources["TextDark"] = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                Resources["TextGray"] = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                Resources["BorderColor"] = new SolidColorBrush(Color.FromRgb(51, 65, 85));

                // Przywróć ciemne statusy
                Resources["StatusSuccessBg"] = new SolidColorBrush(Color.FromRgb(20, 83, 45));
                Resources["StatusWarningBg"] = new SolidColorBrush(Color.FromRgb(120, 53, 15));
                Resources["StatusInfoBg"] = new SolidColorBrush(Color.FromRgb(30, 58, 138));
                Resources["StatusDangerBg"] = new SolidColorBrush(Color.FromRgb(127, 29, 29));
                Resources["StatusNeutralBg"] = new SolidColorBrush(Color.FromRgb(55, 65, 81));
                Resources["StatusTealBg"] = new SolidColorBrush(Color.FromRgb(19, 78, 74));

                dgKontakty.Background = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                dgKontakty.RowBackground = new SolidColorBrush(Color.FromRgb(30, 41, 59));
                dgKontakty.AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(39, 53, 72));
                dgKontakty.GridLinesVisibility = DataGridGridLinesVisibility.None;
                dgKontakty.BorderThickness = new Thickness(0);

                // Restore dark DataGrid header style
                var headerStyleDk = new Style(typeof(DataGridColumnHeader));
                headerStyleDk.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty, new SolidColorBrush(Color.FromRgb(15, 23, 42))));
                headerStyleDk.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty, new SolidColorBrush(Color.FromRgb(148, 163, 184))));
                headerStyleDk.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(12, 14, 12, 14)));
                headerStyleDk.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty, FontWeights.SemiBold));
                headerStyleDk.Setters.Add(new Setter(DataGridColumnHeader.FontSizeProperty, 13.0));
                headerStyleDk.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0, 0, 0, 2)));
                headerStyleDk.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(51, 65, 85))));
                headerStyleDk.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
                dgKontakty.ColumnHeaderStyle = headerStyleDk;

                var cellStyleDk = new Style(typeof(DataGridCell));
                cellStyleDk.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)));
                cellStyleDk.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty, null));
                cellStyleDk.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(8, 4, 8, 4)));
                cellStyleDk.Setters.Add(new Setter(DataGridCell.BackgroundProperty, Brushes.Transparent));
                cellStyleDk.Setters.Add(new Setter(DataGridCell.ForegroundProperty, new SolidColorBrush(Color.FromRgb(226, 232, 240))));
                dgKontakty.CellStyle = cellStyleDk;

                var rowStyleDk = new Style(typeof(DataGridRow));
                rowStyleDk.Setters.Add(new Setter(DataGridRow.HeightProperty, 60.0));
                rowStyleDk.Setters.Add(new Setter(DataGridRow.FontSizeProperty, 14.0));
                rowStyleDk.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(30, 41, 59))));
                rowStyleDk.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)));
                rowStyleDk.Setters.Add(new Setter(DataGridRow.BorderBrushProperty, Brushes.Transparent));
                var hoverDk = new Trigger { Property = DataGridRow.IsMouseOverProperty, Value = true };
                hoverDk.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(51, 65, 85))));
                hoverDk.Setters.Add(new Setter(DataGridRow.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(99, 102, 241))));
                hoverDk.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(2)));
                rowStyleDk.Triggers.Add(hoverDk);
                var selectedDk = new Trigger { Property = DataGridRow.IsSelectedProperty, Value = true };
                selectedDk.Setters.Add(new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromArgb(40, 99, 102, 241))));
                selectedDk.Setters.Add(new Setter(DataGridRow.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(129, 140, 248)))); // indigo-400
                selectedDk.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty, new Thickness(2)));
                rowStyleDk.Triggers.Add(selectedDk);
                dgKontakty.RowStyle = rowStyleDk;

                ApplyThemeToTree(this, false);
            }
        }

        private void ApplyThemeToTree(DependencyObject root, bool isLight)
        {
            // ── Light palette — wysoki kontrast, czytelny czarny tekst ──
            var ltBlack = new SolidColorBrush(Color.FromRgb(10, 10, 10));           // prawie czarny
            var ltTextPrimary = new SolidColorBrush(Color.FromRgb(15, 23, 42));     // #0F172A
            var ltTextSecondary = new SolidColorBrush(Color.FromRgb(51, 65, 85));   // #334155
            var ltTextMuted = new SolidColorBrush(Color.FromRgb(71, 85, 105));      // #475569
            var ltBg = Color.FromRgb(255, 255, 255);
            var ltBgCard = Color.FromRgb(248, 250, 252);      // #F8FAFC
            var ltBgPanel = Color.FromRgb(241, 245, 249);     // #F1F5F9
            var ltBgNested = Color.FromRgb(226, 232, 240);    // #E2E8F0
            var ltBorder = Color.FromRgb(203, 213, 225);      // #CBD5E1

            // ── Dark palette (original) ──
            var dkBg = Color.FromRgb(30, 41, 59);
            var dkBgDeep = Color.FromRgb(15, 23, 42);
            var dkBorder = Color.FromRgb(51, 65, 85);
            var dkTextPrimary = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            var dkTextSecondary = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            var dkAccentLight = new SolidColorBrush(Color.FromRgb(165, 180, 252));

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);

                if (child is TextBlock tb)
                {
                    if (tb.Foreground is SolidColorBrush brush)
                    {
                        var c = brush.Color;
                        if (isLight)
                        {
                            // White/near-white → czarny
                            if (c.R > 220 && c.G > 220 && c.B > 220 && c.A > 200) tb.Foreground = ltBlack;
                            // #E2E8F0 → czarny
                            else if (c.R == 226 && c.G == 232 && c.B == 240) tb.Foreground = ltBlack;
                            // #94A3B8 → ciemny szary
                            else if (c.R == 148 && c.G == 163 && c.B == 184) tb.Foreground = ltTextSecondary;
                            // #64748B → muted
                            else if (c.R == 100 && c.G == 116 && c.B == 139) tb.Foreground = ltTextMuted;
                            // Semi-transparent white → proper dark text based on alpha
                            else if (c.R > 200 && c.G > 200 && c.B > 200 && c.A > 100 && c.A <= 230)
                                tb.Foreground = ltTextPrimary;
                            else if (c.R > 200 && c.G > 200 && c.B > 200 && c.A > 30 && c.A <= 100)
                                tb.Foreground = ltTextSecondary;
                            // Light grays 130-200 → muted
                            else if (c.R > 130 && c.G > 130 && c.B > 130 && c.R <= 220 && c.A > 100)
                                tb.Foreground = ltTextMuted;
                            // #A5B4FC indigo light → dark blue
                            else if (c.R == 165 && c.G == 180 && c.B == 252)
                                tb.Foreground = new SolidColorBrush(Color.FromRgb(55, 48, 163)); // #3730A3
                            // #6366F1 indigo → darker
                            else if (c.R == 99 && c.G == 102 && c.B == 241)
                                tb.Foreground = new SolidColorBrush(Color.FromRgb(67, 56, 202)); // #4338CA
                            // Green accent → darker green
                            else if (c.R == 34 && c.G == 197 && c.B == 94)
                                tb.Foreground = new SolidColorBrush(Color.FromRgb(21, 128, 61)); // #15803D
                        }
                        else
                        {
                            // Reverse: black/dark → white
                            if (c.R < 30 && c.G < 30 && c.B < 30 && c.A > 150) tb.Foreground = dkTextPrimary;
                            else if (c.R == 15 && c.G == 23 && c.B == 42) tb.Foreground = dkTextPrimary;
                            else if (c.R == 51 && c.G == 65 && c.B == 85) tb.Foreground = dkTextSecondary;
                            else if (c.R == 71 && c.G == 85 && c.B == 105) tb.Foreground = dkTextSecondary;
                            else if (c.R == 21 && c.G == 128 && c.B == 61) tb.Foreground = dkAccentLight;
                            else if (c.R == 37 && c.G == 99 && c.B == 235) tb.Foreground = dkAccentLight;
                            else if (c.R == 55 && c.G == 48 && c.B == 163) // #3730A3 → #A5B4FC
                                tb.Foreground = new SolidColorBrush(Color.FromRgb(165, 180, 252));
                            else if (c.R == 67 && c.G == 56 && c.B == 202) // #4338CA → #6366F1
                                tb.Foreground = new SolidColorBrush(Color.FromRgb(99, 102, 241));
                        }
                    }
                }
                else if (child is Border border)
                {
                    if (border.Background is SolidColorBrush bg)
                    {
                        var c = bg.Color;
                        if (isLight)
                        {
                            // #1E293B → white
                            if (c.R == 30 && c.G == 41 && c.B == 59) border.Background = new SolidColorBrush(ltBg);
                            // #0F172A → panel gray
                            else if (c.R == 15 && c.G == 23 && c.B == 42) border.Background = new SolidColorBrush(ltBgNested);
                            // #334155 → lighter
                            else if (c.R == 51 && c.G == 65 && c.B == 85) border.Background = new SolidColorBrush(ltBgPanel);
                            // #475569 → medium
                            else if (c.R == 71 && c.G == 85 && c.B == 105) border.Background = new SolidColorBrush(ltBgNested);
                            // #273548 → alt row
                            else if (c.R == 39 && c.G == 53 && c.B == 72) border.Background = new SolidColorBrush(ltBgCard);
                            // Semi-transparent → light solid
                            else if (c.A < 60 && c.A > 0 && c.R > 200) border.Background = new SolidColorBrush(ltBgPanel);
                        }
                        else
                        {
                            if (c.R == 255 && c.G == 255 && c.B == 255 && c.A > 200) border.Background = new SolidColorBrush(dkBg);
                            else if (c.R == 248 && c.G == 250 && c.B == 252) border.Background = new SolidColorBrush(Color.FromRgb(39, 53, 72));
                            else if (c.R == 241 && c.G == 245 && c.B == 249) border.Background = new SolidColorBrush(dkBg);
                            else if (c.R == 226 && c.G == 232 && c.B == 240) border.Background = new SolidColorBrush(dkBgDeep);
                            else if (c.R == 203 && c.G == 213 && c.B == 225) border.Background = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                        }
                    }
                    if (border.BorderBrush is SolidColorBrush bb)
                    {
                        var c = bb.Color;
                        if (isLight)
                        {
                            if (c.R == 51 && c.G == 65 && c.B == 85) border.BorderBrush = new SolidColorBrush(ltBorder);
                            else if (c.R == 71 && c.G == 85 && c.B == 105) border.BorderBrush = new SolidColorBrush(ltBorder);
                            // Semi-transparent borders → solid
                            else if (c.A < 100 && c.R > 200) border.BorderBrush = new SolidColorBrush(ltBorder);
                        }
                        else
                        {
                            if (c.R == 203 && c.G == 213 && c.B == 225) border.BorderBrush = new SolidColorBrush(dkBorder);
                            else if (c.R == 226 && c.G == 232 && c.B == 240) border.BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                        }
                    }
                }
                else if (child is TextBox textBox)
                {
                    if (isLight)
                    {
                        if (textBox.Foreground is SolidColorBrush tbBrush && tbBrush.Color.R > 180)
                            textBox.Foreground = ltBlack;
                        if (textBox.Background is SolidColorBrush txBg && txBg.Color.A == 0)
                        { /* transparent stays */ }
                        else if (textBox.Background is SolidColorBrush txBg2 && txBg2.Color.R < 60)
                            textBox.Background = new SolidColorBrush(ltBg);
                        if (textBox.CaretBrush is SolidColorBrush cb && cb.Color.R > 200)
                            textBox.CaretBrush = ltBlack;
                    }
                    else
                    {
                        textBox.Foreground = dkTextPrimary;
                        textBox.CaretBrush = dkTextPrimary;
                    }
                }

                ApplyThemeToTree(child, isLight);
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