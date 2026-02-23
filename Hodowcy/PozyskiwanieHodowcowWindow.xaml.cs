using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Kalendarz1.Hodowcy
{
    public partial class PozyskiwanieHodowcowWindow : Window
    {
        private readonly string connectionString = "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True";

        private DataTable dtHodowcy;
        private int aktualnyHodowcaId = 0;
        private bool isLoading = false;
        private bool isFilterUpdating = false;
        private DispatcherTimer _autoRefreshTimer;

        // Avatar cache
        private readonly Dictionary<string, BitmapSource> _avatarCache = new();
        private static readonly Random _rng = new();

        // Szablony rozmów telefonicznych
        private static readonly string[] _szablonyRozmow = new[]
        {
            "Dzień dobry, tu {nazwa} z Ubojni Drobiu Piórkowscy. Dzwonię, bo szukamy nowych hodowców do współpracy. Chciałbym zapytać — czy prowadzi Pan/Pani hodowlę na wolnym rynku, czy może jest Pan/Pani obecnie na kontrakcie?",

            "Dzień dobry! Nazywam się {nazwa}, dzwonię z firmy Ubojnia Drobiu Piórkowscy. Dostaliśmy kontakt do Pana/Pani jako hodowcy drobiu i chciałbym zapytać, czy byłaby możliwość rozmowy o współpracy? Potrzebujemy pilnie dostaw na najbliższe tygodnie.",

            "Dzień dobry, z tej strony {nazwa} — Ubojnia Drobiu Piórkowscy. Rozbudowujemy naszą bazę dostawców i szukamy hodowców, z którymi moglibyśmy nawiązać stałą współpracę. Czy jest Pan/Pani zainteresowany/a rozmową na ten temat?",

            "Dzień dobry! Jestem {nazwa} z Ubojni Drobiu Piórkowscy. Aktualnie bardzo potrzebujemy dostawców drobiu na nadchodzące tygodnie. Czy funkcjonuje Pan/Pani na wolnym rynku i czy moglibyśmy porozmawiać o warunkach współpracy?",

            "Dzień dobry, {nazwa} z Piórkowskich — Ubojnia Drobiu. Kontaktuję się, ponieważ poszukujemy hodowców drobiu do regularnej współpracy. Oferujemy uczciwe warunki i terminowe płatności. Czy jest Pan/Pani otwarty/a na rozmowę?",

            "Dzień dobry! Tu {nazwa}, Ubojnia Drobiu Piórkowscy. Zwracam się do Pana/Pani, bo poszukujemy nowych dostawców żywca. Mamy duże zapotrzebowanie na najbliższe tygodnie. Czy hoduje Pan/Pani aktualnie drób i czy możemy porozmawiać o współpracy?",

            "Dzień dobry, nazywam się {nazwa} i dzwonię z Ubojni Drobiu Piórkowscy. Jesteśmy solidną firmą z wieloletnim doświadczeniem i szukamy hodowców do długofalowej współpracy. Czy mogę zapytać, jaka jest Pana/Pani aktualna sytuacja — wolny rynek czy kontrakt?",

            "Dzień dobry! {nazwa} z Ubojni Piórkowscy. Szukamy hodowców drobiu, którzy chcieliby z nami współpracować. Gwarantujemy odbiór, dobre ceny i regularność. Czy jest Pan/Pani zainteresowany/a? Chętnie opowiem więcej o warunkach."
        };

        // Mapowanie prefixów kodu pocztowego do województw
        private static readonly Dictionary<string, string> kodDoWojewodztwa = new()
        {
            {"00","Mazowieckie"},{"01","Mazowieckie"},{"02","Mazowieckie"},{"03","Mazowieckie"},{"04","Mazowieckie"},{"05","Mazowieckie"},
            {"06","Mazowieckie"},{"07","Mazowieckie"},{"08","Mazowieckie"},{"09","Mazowieckie"},
            {"10","Warmińsko-Mazurskie"},{"11","Warmińsko-Mazurskie"},{"12","Warmińsko-Mazurskie"},{"13","Warmińsko-Mazurskie"},{"14","Warmińsko-Mazurskie"},
            {"15","Podlaskie"},{"16","Podlaskie"},{"17","Podlaskie"},{"18","Podlaskie"},{"19","Podlaskie"},
            {"20","Lubelskie"},{"21","Lubelskie"},{"22","Lubelskie"},{"23","Lubelskie"},{"24","Lubelskie"},
            {"25","Świętokrzyskie"},{"26","Świętokrzyskie"},{"27","Świętokrzyskie"},{"28","Świętokrzyskie"},{"29","Świętokrzyskie"},
            {"30","Małopolskie"},{"31","Małopolskie"},{"32","Małopolskie"},{"33","Małopolskie"},{"34","Małopolskie"},
            {"35","Podkarpackie"},{"36","Podkarpackie"},{"37","Podkarpackie"},{"38","Podkarpackie"},{"39","Podkarpackie"},
            {"40","Śląskie"},{"41","Śląskie"},{"42","Śląskie"},{"43","Śląskie"},{"44","Śląskie"},
            {"45","Opolskie"},{"46","Opolskie"},{"47","Opolskie"},{"48","Opolskie"},{"49","Opolskie"},
            {"50","Dolnośląskie"},{"51","Dolnośląskie"},{"52","Dolnośląskie"},{"53","Dolnośląskie"},{"54","Dolnośląskie"},
            {"55","Dolnośląskie"},{"56","Dolnośląskie"},{"57","Dolnośląskie"},{"58","Dolnośląskie"},{"59","Dolnośląskie"},
            {"60","Wielkopolskie"},{"61","Wielkopolskie"},{"62","Wielkopolskie"},{"63","Wielkopolskie"},{"64","Wielkopolskie"},
            {"65","Lubuskie"},{"66","Lubuskie"},{"67","Lubuskie"},{"68","Lubuskie"},{"69","Lubuskie"},
            {"70","Zachodniopomorskie"},{"71","Zachodniopomorskie"},{"72","Zachodniopomorskie"},{"73","Zachodniopomorskie"},{"74","Zachodniopomorskie"},
            {"75","Zachodniopomorskie"},{"76","Zachodniopomorskie"},{"77","Pomorskie"},{"78","Zachodniopomorskie"},
            {"80","Pomorskie"},{"81","Pomorskie"},{"82","Pomorskie"},{"83","Pomorskie"},{"84","Pomorskie"},
            {"85","Kujawsko-Pomorskie"},{"86","Kujawsko-Pomorskie"},{"87","Kujawsko-Pomorskie"},{"88","Kujawsko-Pomorskie"},{"89","Kujawsko-Pomorskie"},
            {"90","Łódzkie"},{"91","Łódzkie"},{"92","Łódzkie"},{"93","Łódzkie"},{"94","Łódzkie"},
            {"95","Łódzkie"},{"96","Łódzkie"},{"97","Łódzkie"},{"98","Łódzkie"},{"99","Łódzkie"}
        };

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        public PozyskiwanieHodowcowWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            Loaded += Window_Loaded;
            dgHodowcy.LoadingRow += DgHodowcy_LoadingRow;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Load current user avatar + name
            txtUserName.Text = App.UserFullName ?? App.UserID ?? "";
            try
            {
                var avatar = GetUserAvatar(App.UserID, App.UserFullName, 32);
                if (avatar != null)
                    imgUserAvatar.Source = avatar;
            }
            catch { }

            // Fix: zmień wszystkie "(brak)" Towar na "KURCZAKI" w bazie
            try
            {
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();
                    using var cmd = new SqlCommand("UPDATE Pozyskiwanie_Hodowcy SET Towar = 'KURCZAKI' WHERE Towar IS NULL OR Towar = '' OR Towar = '(brak)'", conn);
                    cmd.ExecuteNonQuery();
                });
            }
            catch { }

            InitializeFilters();
            UstawSzablonRozmowy();
            await LoadDataAsync();

            // Auto-refresh co 2 minuty
            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            _autoRefreshTimer.Tick += AutoRefresh_Tick;
            _autoRefreshTimer.Start();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F5)
            {
                e.Handled = true;
                BtnRefresh_Click(sender, e);
            }
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await LoadDataAsync();
        }

        private async void AutoRefresh_Tick(object sender, EventArgs e)
        {
            if (isLoading) return;

            // Zapamiętaj aktualną pozycję i filtry
            int selectedId = aktualnyHodowcaId;
            string filterTowar = cmbTowar.SelectedItem?.ToString();
            string filterStatus = cmbStatus.SelectedItem?.ToString();
            string filterWoj = cmbWojewodztwo?.SelectedItem?.ToString();
            string filterPowiat = cmbPowiat?.SelectedItem?.ToString();
            string filterSzukaj = txtSzukaj.Text;

            // Zapamiętaj scroll position DataGrid
            var scrollViewer = FindVisualChild<ScrollViewer>(dgHodowcy);
            double verticalOffset = scrollViewer?.VerticalOffset ?? 0;

            await LoadDataAsync();

            // Przywróć filtry
            isFilterUpdating = true;
            try
            {
                if (filterTowar != null) for (int i = 0; i < cmbTowar.Items.Count; i++) { if (cmbTowar.Items[i]?.ToString() == filterTowar) { cmbTowar.SelectedIndex = i; break; } }
                if (filterStatus != null) for (int i = 0; i < cmbStatus.Items.Count; i++) { if (cmbStatus.Items[i]?.ToString() == filterStatus) { cmbStatus.SelectedIndex = i; break; } }
                if (filterWoj != null) for (int i = 0; i < cmbWojewodztwo.Items.Count; i++) { if (cmbWojewodztwo.Items[i]?.ToString() == filterWoj) { cmbWojewodztwo.SelectedIndex = i; break; } }
                if (filterPowiat != null) for (int i = 0; i < cmbPowiat.Items.Count; i++) { if (cmbPowiat.Items[i]?.ToString() == filterPowiat) { cmbPowiat.SelectedIndex = i; break; } }
                txtSzukaj.Text = filterSzukaj;
            }
            finally { isFilterUpdating = false; }

            ApplyFilters();

            // Przywróć zaznaczenie
            if (selectedId > 0 && dtHodowcy != null)
            {
                foreach (DataRowView drv in dtHodowcy.DefaultView)
                {
                    if (Convert.ToInt32(drv["Id"]) == selectedId)
                    {
                        dgHodowcy.SelectedItem = drv;
                        dgHodowcy.ScrollIntoView(drv);
                        break;
                    }
                }
            }

            // Przywróć scroll position
            if (scrollViewer != null)
            {
                Dispatcher.BeginInvoke(new Action(() => scrollViewer.ScrollToVerticalOffset(verticalOffset)), DispatcherPriority.Loaded);
            }
        }


        #region Filters

        private void InitializeFilters()
        {
            cmbTowar.Items.Add("Wszystkie");
            cmbTowar.Items.Add("KURCZAKI");
            cmbTowar.Items.Add("DROB");
            cmbTowar.Items.Add("GESI");
            cmbTowar.Items.Add("KACZKI");
            cmbTowar.Items.Add("PERLICZKI");
            cmbTowar.SelectedIndex = 1; // domyślnie KURCZAKI

            cmbStatus.Items.Add("Wszystkie");
            cmbStatus.Items.Add("Nowy");
            cmbStatus.Items.Add("Do zadzwonienia");
            cmbStatus.Items.Add("Próba kontaktu");
            cmbStatus.Items.Add("Nawiązano kontakt");
            cmbStatus.Items.Add("Zdaje");
            cmbStatus.Items.Add("Nie zainteresowany");
            cmbStatus.Items.Add("Obcy kontrakt");
            cmbStatus.SelectedIndex = 0;

            // Województwa
            cmbWojewodztwo.Items.Add("Wszystkie");
            foreach (var w in kodDoWojewodztwa.Values.Distinct().OrderBy(x => x))
                cmbWojewodztwo.Items.Add(w);
            cmbWojewodztwo.SelectedIndex = 0;

            // Powiaty — wypełniane dynamicznie po wyborze województwa
            cmbPowiat.Items.Add("Wszystkie");
            cmbPowiat.SelectedIndex = 0;
        }

        private void CmbWojewodztwo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isLoading || isFilterUpdating) return;

            // Odśwież listę powiatów dla wybranego województwa
            string woj = cmbWojewodztwo.SelectedItem?.ToString();
            isFilterUpdating = true;
            cmbPowiat.Items.Clear();
            cmbPowiat.Items.Add("Wszystkie");

            if (!string.IsNullOrEmpty(woj) && woj != "Wszystkie" && dtHodowcy != null)
            {
                var powiaty = dtHodowcy.AsEnumerable()
                    .Where(r => r["Wojewodztwo"]?.ToString() == woj)
                    .Select(r => r["Powiat"]?.ToString())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct()
                    .OrderBy(p => p);
                foreach (var p in powiaty)
                    cmbPowiat.Items.Add(p);
            }

            cmbPowiat.SelectedIndex = 0;
            isFilterUpdating = false;
            ApplyFilters();
        }

        private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isLoading && !isFilterUpdating) ApplyFilters();
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!isLoading) ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (dtHodowcy == null) return;

            var filters = new List<string>();

            // Towar
            string towar = cmbTowar.SelectedItem?.ToString();
            if (towar != null && towar != "Wszystkie")
                filters.Add($"Towar = '{towar}'");

            // Status
            string status = cmbStatus.SelectedItem?.ToString();
            if (status != null && status != "Wszystkie")
                filters.Add($"Status = '{status}'");

            // Województwo
            string woj = cmbWojewodztwo?.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(woj) && woj != "Wszystkie")
                filters.Add($"Wojewodztwo = '{woj.Replace("'", "''")}'");

            // Powiat
            string powiat = cmbPowiat?.SelectedItem?.ToString();
            if (!string.IsNullOrEmpty(powiat) && powiat != "Wszystkie")
                filters.Add($"Powiat = '{powiat.Replace("'", "''")}'");

            // Szukaj
            string szukaj = txtSzukaj.Text?.Trim();
            if (!string.IsNullOrEmpty(szukaj))
            {
                string s = szukaj.Replace("'", "''");
                filters.Add($"(Dostawca LIKE '%{s}%' OR Miejscowosc LIKE '%{s}%' OR Tel1 LIKE '%{s}%' OR Tel2 LIKE '%{s}%')");
            }

            string filter = filters.Count > 0 ? string.Join(" AND ", filters) : "";
            try
            {
                dtHodowcy.DefaultView.RowFilter = filter;
            }
            catch
            {
                dtHodowcy.DefaultView.RowFilter = "";
            }

            txtLiczbaWynikow.Text = $"{dtHodowcy.DefaultView.Count} hodowców";
        }

        #endregion

        #region Data Loading

        private async Task LoadDataAsync()
        {
            isLoading = true;
            loadingOverlay.Visibility = Visibility.Visible;

            try
            {
                dtHodowcy = new DataTable();
                dtHodowcy.Columns.Add("Id", typeof(int));
                dtHodowcy.Columns.Add("Dostawca", typeof(string));
                dtHodowcy.Columns.Add("Towar", typeof(string));
                dtHodowcy.Columns.Add("Ulica", typeof(string));
                dtHodowcy.Columns.Add("KodPocztowy", typeof(string));
                dtHodowcy.Columns.Add("Miejscowosc", typeof(string));
                dtHodowcy.Columns.Add("KM", typeof(decimal));
                dtHodowcy.Columns.Add("Tel1", typeof(string));
                dtHodowcy.Columns.Add("Tel2", typeof(string));
                dtHodowcy.Columns.Add("Tel3", typeof(string));
                dtHodowcy.Columns.Add("Status", typeof(string));
                dtHodowcy.Columns.Add("Kontrakt", typeof(string));
                dtHodowcy.Columns.Add("Notatka", typeof(string));
                dtHodowcy.Columns.Add("PrzypisanyDo", typeof(string));
                dtHodowcy.Columns.Add("PrzypisanyNazwa", typeof(string));
                dtHodowcy.Columns.Add("DataOstatniegoKontaktu", typeof(DateTime));
                dtHodowcy.Columns.Add("DataNastepnegoKontaktu", typeof(DateTime));
                // Ostatnia aktywność
                dtHodowcy.Columns.Add("Tel1Display", typeof(string));
                dtHodowcy.Columns.Add("Tel2Display", typeof(string));
                dtHodowcy.Columns.Add("Wojewodztwo", typeof(string));
                dtHodowcy.Columns.Add("Powiat", typeof(string));
                // Ostatnia aktywność
                dtHodowcy.Columns.Add("OstatniUserId", typeof(string));
                dtHodowcy.Columns.Add("OstatniUserNazwa", typeof(string));
                dtHodowcy.Columns.Add("OstatniaTresc", typeof(string));
                dtHodowcy.Columns.Add("OstatniaData", typeof(DateTime));
                dtHodowcy.Columns.Add("IsDuplicate", typeof(bool));

                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();

                    string sql = @"SELECT h.Id, h.Dostawca, h.Towar, h.Ulica, h.KodPocztowy, h.Miejscowosc, h.KM,
                                          h.Tel1, h.Tel2, h.Tel3, h.Status, h.Kontrakt, h.Notatka,
                                          h.PrzypisanyDo, h.DataOstatniegoKontaktu, h.DataNastepnegoKontaktu,
                                          la.UzytkownikId AS OstatniUserId,
                                          la.UzytkownikNazwa AS OstatniUserNazwa,
                                          la.Tresc AS OstatniaTresc,
                                          la.DataUtworzenia AS OstatniaData
                                   FROM Pozyskiwanie_Hodowcy h
                                   OUTER APPLY (
                                       SELECT TOP 1 a.UzytkownikId, a.UzytkownikNazwa, a.Tresc, a.DataUtworzenia
                                       FROM Pozyskiwanie_Aktywnosci a
                                       WHERE a.HodowcaId = h.Id
                                       ORDER BY a.DataUtworzenia DESC
                                   ) la
                                   WHERE h.Aktywny = 1
                                   ORDER BY h.Dostawca";

                    using var cmd = new SqlCommand(sql, conn);
                    using var reader = cmd.ExecuteReader();

                    // Pobierz mapowanie KodPocztowy → Powiat z OdbiorcyCRM
                    var powiatMap = new Dictionary<string, string>();
                    try
                    {
                        using var connP = new SqlConnection(connectionString);
                        connP.Open();
                        using var cmdP = new SqlCommand("SELECT DISTINCT REPLACE(KOD, '-', '') AS Kod, Powiat FROM OdbiorcyCRM WHERE Powiat IS NOT NULL AND LEN(Powiat) > 0 AND KOD IS NOT NULL", connP);
                        using var readerP = cmdP.ExecuteReader();
                        while (readerP.Read())
                        {
                            string k = readerP["Kod"]?.ToString()?.Trim();
                            string p = readerP["Powiat"]?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(p) && !powiatMap.ContainsKey(k))
                                powiatMap[k] = p;
                        }
                    }
                    catch { }

                    // Pobierz nazwy użytkowników
                    var userNames = new Dictionary<string, string>();
                    try
                    {
                        using var conn2 = new SqlConnection(connectionString);
                        conn2.Open();
                        using var cmd2 = new SqlCommand("SELECT UserID, UserFullName FROM Users", conn2);
                        using var reader2 = cmd2.ExecuteReader();
                        while (reader2.Read())
                        {
                            string uid = reader2["UserID"]?.ToString()?.Trim();
                            string uname = reader2["UserFullName"]?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(uid))
                                userNames[uid] = uname ?? uid;
                        }
                    }
                    catch { }

                    while (reader.Read())
                    {
                        var row = dtHodowcy.NewRow();
                        row["Id"] = reader["Id"];
                        row["Dostawca"] = reader["Dostawca"]?.ToString()?.Trim() ?? "";
                        row["Towar"] = reader["Towar"] is DBNull ? "" : reader["Towar"]?.ToString()?.Trim() ?? "";
                        row["Ulica"] = reader["Ulica"] is DBNull ? "" : reader["Ulica"]?.ToString()?.Trim() ?? "";
                        row["KodPocztowy"] = reader["KodPocztowy"] is DBNull ? "" : reader["KodPocztowy"]?.ToString()?.Trim() ?? "";
                        row["Miejscowosc"] = reader["Miejscowosc"] is DBNull ? "" : reader["Miejscowosc"]?.ToString()?.Trim() ?? "";
                        row["KM"] = reader["KM"] is DBNull ? DBNull.Value : reader["KM"];
                        string tel1Raw = reader["Tel1"] is DBNull ? "" : reader["Tel1"]?.ToString()?.Trim() ?? "";
                        string tel2Raw = reader["Tel2"] is DBNull ? "" : reader["Tel2"]?.ToString()?.Trim() ?? "";
                        string tel3Raw = reader["Tel3"] is DBNull ? "" : reader["Tel3"]?.ToString()?.Trim() ?? "";
                        row["Tel1"] = tel1Raw;
                        row["Tel2"] = tel2Raw;
                        row["Tel3"] = tel3Raw;

                        // Parse all phone numbers (handles comma-separated and concatenated)
                        var allNumbers = SplitPhoneNumbers(tel1Raw);
                        foreach (var n in SplitPhoneNumbers(tel2Raw))
                            if (!allNumbers.Contains(n)) allNumbers.Add(n);
                        foreach (var n in SplitPhoneNumbers(tel3Raw))
                            if (!allNumbers.Contains(n)) allNumbers.Add(n);

                        row["Tel1Display"] = allNumbers.Count > 0 ? FormatPhone(allNumbers[0]) : "";
                        row["Tel2Display"] = allNumbers.Count > 1 ? FormatPhone(allNumbers[1]) : "";

                        // Województwo z kodu pocztowego + Powiat z OdbiorcyCRM
                        string kodPoczt = row["KodPocztowy"]?.ToString()?.Replace("-", "")?.Trim() ?? "";
                        if (kodPoczt.Length >= 2 && kodDoWojewodztwa.TryGetValue(kodPoczt.Substring(0, 2), out var woj))
                            row["Wojewodztwo"] = woj;
                        else
                            row["Wojewodztwo"] = "";

                        if (!string.IsNullOrEmpty(kodPoczt) && powiatMap.TryGetValue(kodPoczt, out var powiat))
                            row["Powiat"] = powiat;
                        else
                            row["Powiat"] = "";
                        row["Status"] = reader["Status"] is DBNull ? "Nowy" : reader["Status"]?.ToString()?.Trim() ?? "Nowy";
                        row["Kontrakt"] = reader["Kontrakt"] is DBNull ? "" : reader["Kontrakt"]?.ToString()?.Trim() ?? "";
                        row["Notatka"] = reader["Notatka"] is DBNull ? "" : reader["Notatka"]?.ToString()?.Trim() ?? "";

                        string przypisanyDo = reader["PrzypisanyDo"] is DBNull ? "" : reader["PrzypisanyDo"]?.ToString()?.Trim() ?? "";
                        row["PrzypisanyDo"] = przypisanyDo;
                        row["PrzypisanyNazwa"] = !string.IsNullOrEmpty(przypisanyDo) && userNames.TryGetValue(przypisanyDo, out var uname) ? uname : przypisanyDo;

                        row["DataOstatniegoKontaktu"] = reader["DataOstatniegoKontaktu"] is DBNull ? DBNull.Value : reader["DataOstatniegoKontaktu"];
                        row["DataNastepnegoKontaktu"] = reader["DataNastepnegoKontaktu"] is DBNull ? DBNull.Value : reader["DataNastepnegoKontaktu"];

                        // Ostatnia aktywność (ukryj "Import z Excel")
                        string ostatniUid = reader["OstatniUserId"] is DBNull ? "" : reader["OstatniUserId"]?.ToString()?.Trim() ?? "";
                        bool isImport = ostatniUid == "IMPORT";
                        row["OstatniUserId"] = isImport ? "" : ostatniUid;
                        row["OstatniUserNazwa"] = isImport ? "" : (reader["OstatniUserNazwa"] is DBNull ? "" : reader["OstatniUserNazwa"]?.ToString()?.Trim() ?? "");
                        row["OstatniaTresc"] = reader["OstatniaTresc"] is DBNull ? "" : reader["OstatniaTresc"]?.ToString()?.Trim() ?? "";
                        row["OstatniaData"] = reader["OstatniaData"] is DBNull ? DBNull.Value : reader["OstatniaData"];

                        dtHodowcy.Rows.Add(row);
                    }
                });

                // Oznacz duplikaty (ta sama nazwa Dostawca)
                var nameCounts = dtHodowcy.AsEnumerable()
                    .GroupBy(r => (r["Dostawca"]?.ToString() ?? "").ToLowerInvariant().Trim())
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToHashSet();
                foreach (DataRow row in dtHodowcy.Rows)
                {
                    string nazwa = (row["Dostawca"]?.ToString() ?? "").ToLowerInvariant().Trim();
                    row["IsDuplicate"] = nameCounts.Contains(nazwa);
                }

                dgHodowcy.ItemsSource = dtHodowcy.DefaultView;

                // Sortowanie domyślne: KM od najmniejszego
                dtHodowcy.DefaultView.Sort = "KM ASC";
                ApplyFilters();
                LoadKpiUsers();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isLoading = false;
                loadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private async void LoadKpiUsers()
        {
            panelKpiUsers.Children.Clear();

            try
            {
                var stats = new List<(string UserId, string UserName, int DzisTotal, int TydzTotal, int Notatki, int Statusy)>();

                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();

                    string sql = @"SET DATEFIRST 1;
                                   DECLARE @pon date = DATEADD(day, 1-DATEPART(weekday, GETDATE()), CONVERT(date, GETDATE()));
                                   SELECT UzytkownikId, UzytkownikNazwa,
                                          SUM(CASE WHEN CONVERT(date,DataUtworzenia)=CONVERT(date,GETDATE()) THEN 1 ELSE 0 END) AS Dzis,
                                          COUNT(*) AS Tydzien,
                                          SUM(CASE WHEN TypAktywnosci='Notatka' THEN 1 ELSE 0 END) AS Notatki,
                                          SUM(CASE WHEN TypAktywnosci='Zmiana statusu' THEN 1 ELSE 0 END) AS Statusy
                                   FROM Pozyskiwanie_Aktywnosci
                                   WHERE UzytkownikId != 'IMPORT'
                                     AND TypAktywnosci IN ('Notatka','Zmiana statusu','Przypisanie')
                                     AND DataUtworzenia >= @pon
                                   GROUP BY UzytkownikId, UzytkownikNazwa
                                   ORDER BY SUM(CASE WHEN CONVERT(date,DataUtworzenia)=CONVERT(date,GETDATE()) THEN 1 ELSE 0 END) DESC";

                    using var cmd = new SqlCommand(sql, conn);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        stats.Add((
                            reader["UzytkownikId"]?.ToString()?.Trim() ?? "",
                            reader["UzytkownikNazwa"]?.ToString()?.Trim() ?? "",
                            Convert.ToInt32(reader["Dzis"]),
                            Convert.ToInt32(reader["Tydzien"]),
                            Convert.ToInt32(reader["Notatki"]),
                            Convert.ToInt32(reader["Statusy"])
                        ));
                    }
                });

                foreach (var (userId, userName, dzisTotal, tydzTotal, notatki, statusy) in stats)
                {
                    if (tydzTotal == 0) continue;
                    bool isMe = userId == App.UserID;

                    var card = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B")),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(6, 4, 8, 4),
                        Margin = new Thickness(0, 0, 6, 0),
                        BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isMe ? "#22C55E" : "#334155")),
                        BorderThickness = new Thickness(1),
                        Cursor = Cursors.Hand
                    };

                    string capturedUserId = userId;
                    string capturedUserName = userName;
                    card.MouseLeftButtonDown += (s, args) => ShowUserActivityDetails(capturedUserId, capturedUserName);

                    var sp = new StackPanel { Orientation = Orientation.Horizontal };

                    // Avatar
                    var avatarBorder = new Border
                    {
                        Width = 26, Height = 26,
                        CornerRadius = new CornerRadius(13),
                        ClipToBounds = true,
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#312E81")),
                        Margin = new Thickness(0, 0, 6, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var avatarImg = new System.Windows.Controls.Image { Stretch = System.Windows.Media.Stretch.UniformToFill };
                    avatarImg.Source = GetUserAvatar(userId, userName, 26);
                    avatarBorder.Child = avatarImg;
                    sp.Children.Add(avatarBorder);

                    // Liczniki
                    var numSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                    // Dziś
                    var dzisBlock = new TextBlock
                    {
                        FontSize = 12,
                        FontWeight = FontWeights.Black,
                        Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dzisTotal > 0 ? "#22C55E" : "#64748B"))
                    };
                    dzisBlock.Inlines.Add(new System.Windows.Documents.Run(dzisTotal.ToString()));
                    dzisBlock.Inlines.Add(new System.Windows.Documents.Run("/") { FontSize = 9, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#475569")) });
                    dzisBlock.Inlines.Add(new System.Windows.Documents.Run(tydzTotal.ToString()) { FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tydzTotal > 0 ? "#60A5FA" : "#64748B")) });
                    numSp.Children.Add(dzisBlock);

                    var typeSp = new StackPanel { Orientation = Orientation.Horizontal };
                    typeSp.Children.Add(new TextBlock { Text = $"n:{notatki}", FontSize = 8, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#60A5FA")), Margin = new Thickness(0, 0, 4, 0) });
                    typeSp.Children.Add(new TextBlock { Text = $"s:{statusy}", FontSize = 8, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F59E0B")) });
                    numSp.Children.Add(typeSp);

                    sp.Children.Add(numSp);
                    card.Child = sp;
                    panelKpiUsers.Children.Add(card);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadKpiUsers error: {ex.Message}");
            }
        }

        private async void ShowUserActivityDetails(string userId, string userName)
        {
            var dialog = new Window
            {
                Title = $"Aktywność — {userName}",
                WindowState = WindowState.Maximized,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B")),
                ResizeMode = ResizeMode.CanResize
            };
            WindowIconHelper.SetIcon(dialog);

            var mainGrid = new Grid { Margin = new Thickness(16) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: alerts
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: chart section
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 3: activity list

            // Row 0: Header
            var headerSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var avBorder = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(18), ClipToBounds = true, Margin = new Thickness(0, 0, 10, 0) };
            var avImg = new System.Windows.Controls.Image { Stretch = System.Windows.Media.Stretch.UniformToFill };
            avImg.Source = GetUserAvatar(userId, userName, 36);
            avBorder.Child = avImg;
            headerSp.Children.Add(avBorder);
            var headerNameTxt = new TextBlock { Text = userName, FontSize = 16, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center };
            headerSp.Children.Add(headerNameTxt);
            var statTxt = new TextBlock { Text = "  Ładowanie...", FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")), VerticalAlignment = VerticalAlignment.Center };
            headerSp.Children.Add(statTxt);
            Grid.SetRow(headerSp, 0);
            mainGrid.Children.Add(headerSp);

            // Row 1: Alert panel
            var alertPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            Grid.SetRow(alertPanel, 1);
            mainGrid.Children.Add(alertPanel);

            // Row 2: Chart section (header + chart + info)
            var chartSection = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            chartSection.Children.Add(new TextBlock
            {
                Text = "PORÓWNANIE TYGODNIOWE — WSZYSCY UŻYTKOWNICY",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")),
                Margin = new Thickness(0, 0, 0, 6)
            });
            var chartBorder = new Border { Margin = new Thickness(0, 0, 0, 4) };
            chartSection.Children.Add(chartBorder);
            var chartInfoTxt = new TextBlock
            {
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")),
                Margin = new Thickness(4, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            chartSection.Children.Add(chartInfoTxt);
            Grid.SetRow(chartSection, 2);
            mainGrid.Children.Add(chartSection);

            // Row 3: Activity list (fills remaining space, scrollable)
            var listHeader = new TextBlock
            {
                Text = $"HISTORIA AKTYWNOŚCI — {userName.ToUpper()}",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var listSection = new DockPanel();
            DockPanel.SetDock(listHeader, Dock.Top);
            listSection.Children.Add(listHeader);
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var listStack = new StackPanel();
            scroll.Content = listStack;
            listSection.Children.Add(scroll);
            Grid.SetRow(listSection, 3);
            mainGrid.Children.Add(listSection);

            dialog.Content = mainGrid;
            dialog.Show();

            try
            {
                // === 1. Załaduj dane porównawcze WSZYSTKICH userów ===
                var compData = new List<(string UserId, string UserName, int Notatki, int Statusy, int UniqueHodowcy)>();
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();
                    string sql2 = @"SET DATEFIRST 1;
                                    DECLARE @pon date = DATEADD(day, 1 - DATEPART(weekday, GETDATE()), CONVERT(date, GETDATE()));
                                    SELECT UzytkownikId, UzytkownikNazwa,
                                           SUM(CASE WHEN TypAktywnosci='Notatka' THEN 1 ELSE 0 END) AS Notatki,
                                           SUM(CASE WHEN TypAktywnosci='Zmiana statusu' THEN 1 ELSE 0 END) AS Statusy,
                                           COUNT(DISTINCT HodowcaId) AS UniqueHodowcy
                                    FROM Pozyskiwanie_Aktywnosci
                                    WHERE UzytkownikId != 'IMPORT' AND TypAktywnosci != 'Telefon'
                                      AND DataUtworzenia >= @pon
                                    GROUP BY UzytkownikId, UzytkownikNazwa
                                    ORDER BY COUNT(*) DESC";
                    using var cmd2 = new SqlCommand(sql2, conn);
                    using var r2 = cmd2.ExecuteReader();
                    while (r2.Read())
                    {
                        compData.Add((
                            r2["UzytkownikId"]?.ToString() ?? "",
                            r2["UzytkownikNazwa"]?.ToString() ?? "",
                            Convert.ToInt32(r2["Notatki"]),
                            Convert.ToInt32(r2["Statusy"]),
                            Convert.ToInt32(r2["UniqueHodowcy"])
                        ));
                    }
                });

                // === 2. Śledzenie elementów słupków (do podświetlenia wybranego) ===
                string selectedUserId = userId;
                var barHighlightElements = new List<(string UserId, Border AvatarBorder, Border BarBorder, TextBlock NameTxt)>();

                // Funkcja aktualizacji podświetlenia słupków
                void UpdateBarHighlights(string selId)
                {
                    foreach (var bh in barHighlightElements)
                    {
                        bool sel = bh.UserId == selId;
                        bh.AvatarBorder.BorderBrush = sel
                            ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"))
                            : null;
                        bh.AvatarBorder.BorderThickness = sel ? new Thickness(2) : new Thickness(0);
                        bh.BarBorder.Opacity = sel ? 1.0 : 0.7;
                        bh.NameTxt.FontWeight = sel ? FontWeights.Bold : FontWeights.Normal;
                        bh.NameTxt.Foreground = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(sel ? "#FFFFFF" : "#94A3B8"));
                    }
                }

                // === 3. Funkcja ładowania aktywności dla wybranego usera ===
                async Task RefreshActivities(string uid, string uname)
                {
                    alertPanel.Children.Clear();
                    listStack.Children.Clear();
                    statTxt.Text = "  Ładowanie...";
                    avImg.Source = GetUserAvatar(uid, uname, 36);
                    headerNameTxt.Text = uname;
                    listHeader.Text = $"HISTORIA AKTYWNOŚCI — {uname.ToUpper()}";
                    dialog.Title = $"Aktywność — {uname}";

                    var activities = new List<(string Typ, string Tresc, string Hodowca, int HodowcaId, DateTime Data)>();
                    var fraudChanges = new List<(int HodowcaId, string Hodowca, string StatusPrzed, string StatusPo, DateTime Data)>();

                    await Task.Run(() =>
                    {
                        using var conn = new SqlConnection(connectionString);
                        conn.Open();

                        string sql1 = @"SET DATEFIRST 1;
                            DECLARE @pon date = DATEADD(day, 1 - DATEPART(weekday, GETDATE()), CONVERT(date, GETDATE()));
                            SELECT a.TypAktywnosci, a.Tresc, h.Dostawca, a.HodowcaId, a.DataUtworzenia
                            FROM Pozyskiwanie_Aktywnosci a
                            JOIN Pozyskiwanie_Hodowcy h ON a.HodowcaId = h.Id
                            WHERE a.UzytkownikId = @uid AND a.UzytkownikId != 'IMPORT'
                              AND a.TypAktywnosci != 'Telefon'
                              AND a.DataUtworzenia >= @pon
                            ORDER BY a.DataUtworzenia DESC";
                        using (var cmd = new SqlCommand(sql1, conn))
                        {
                            cmd.Parameters.AddWithValue("@uid", uid);
                            using var reader = cmd.ExecuteReader();
                            while (reader.Read())
                            {
                                activities.Add((
                                    reader["TypAktywnosci"]?.ToString() ?? "",
                                    reader["Tresc"]?.ToString() ?? "",
                                    reader["Dostawca"]?.ToString()?.Trim() ?? "",
                                    Convert.ToInt32(reader["HodowcaId"]),
                                    Convert.ToDateTime(reader["DataUtworzenia"])
                                ));
                            }
                        }

                        if (uid == "11111")
                        {
                            string sql3 = @"SET DATEFIRST 1;
                                DECLARE @pon date = DATEADD(day, 1 - DATEPART(weekday, GETDATE()), CONVERT(date, GETDATE()));
                                SELECT a.HodowcaId, h.Dostawca, a.StatusPrzed, a.StatusPo, a.DataUtworzenia
                                FROM Pozyskiwanie_Aktywnosci a
                                JOIN Pozyskiwanie_Hodowcy h ON a.HodowcaId = h.Id
                                WHERE a.UzytkownikId = @uid AND a.TypAktywnosci = 'Zmiana statusu'
                                  AND a.DataUtworzenia >= @pon
                                ORDER BY a.HodowcaId, a.DataUtworzenia";
                            using var cmd3 = new SqlCommand(sql3, conn);
                            cmd3.Parameters.AddWithValue("@uid", uid);
                            using var r3 = cmd3.ExecuteReader();
                            while (r3.Read())
                                fraudChanges.Add((Convert.ToInt32(r3["HodowcaId"]), r3["Dostawca"]?.ToString()?.Trim() ?? "",
                                    r3["StatusPrzed"]?.ToString() ?? "", r3["StatusPo"]?.ToString() ?? "", Convert.ToDateTime(r3["DataUtworzenia"])));
                        }
                    });

                    // Statystyki
                    int notatkiCount = activities.Count(a => a.Typ == "Notatka");
                    int statusCount = activities.Count(a => a.Typ == "Zmiana statusu");
                    statTxt.Text = $"  Tydzień: {activities.Count} akcji  |  {notatkiCount} notatek, {statusCount} zmian statusu";

                    // Analiza aktywności (userId 11111)
                    if (uid == "11111" && fraudChanges.Count > 0)
                    {
                        var alerts = new List<string>();
                        foreach (var g in fraudChanges.GroupBy(f => f.HodowcaId))
                        {
                            var changes = g.OrderBy(x => x.Data).ToList();
                            string hodowcaName = changes[0].Hodowca;
                            if (changes.Count >= 4)
                                alerts.Add($"Wiele zmian: {hodowcaName} — {changes.Count} zmian statusu w tygodniu");
                            var seq = new List<string> { changes[0].StatusPrzed };
                            foreach (var c in changes) seq.Add(c.StatusPo);
                            for (int i = 0; i < seq.Count - 2; i++)
                            {
                                if (seq[i] == seq[i + 2])
                                {
                                    int chainEnd = i + 2;
                                    while (chainEnd + 1 < seq.Count && seq[chainEnd + 1] == seq[chainEnd - 1]) chainEnd++;
                                    string chain = string.Join("\u2192", seq.Skip(i).Take(chainEnd - i + 1));
                                    alerts.Add(chainEnd - i + 1 >= 4 ? $"Powtarzający się cykl: {hodowcaName} — {chain}" : $"Zmiana w kółko: {hodowcaName} — {chain}");
                                    i = chainEnd - 1;
                                }
                            }
                        }
                        var userData = compData.FirstOrDefault(u => u.UserId == uid);
                        if (userData.UniqueHodowcy > 0 && userData.Statusy > 0)
                        {
                            double ratio = (double)userData.Statusy / userData.UniqueHodowcy;
                            if (ratio > 3) alerts.Add($"Wysoki stosunek: {userData.Statusy} zmian na {userData.UniqueHodowcy} hodowców (śr. {ratio:F1}/hodowcę)");
                        }
                        if (alerts.Count > 0)
                        {
                            var alertBorder = new Border
                            {
                                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E3A5F")),
                                CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 8)
                            };
                            var alertSp = new StackPanel();
                            alertSp.Children.Add(new TextBlock { Text = "ANALIZA AKTYWNOŚCI", FontSize = 11, FontWeight = FontWeights.Bold,
                                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#93C5FD")), Margin = new Thickness(0, 0, 0, 4) });
                            foreach (var alert in alerts)
                                alertSp.Children.Add(new TextBlock { Text = $"\u2022 {alert}", FontSize = 10,
                                    Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BFDBFE")),
                                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 1, 0, 1) });
                            alertBorder.Child = alertSp;
                            alertPanel.Children.Add(alertBorder);
                        }
                    }

                    // === LISTA AKTYWNOŚCI (3 kolumny, klikalne) ===
                    foreach (var dayGroup in activities.GroupBy(a => a.Data.Date).OrderByDescending(g => g.Key))
                    {
                        var dayHdr = new Border
                        {
                            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                            CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 8, 0, 4)
                        };
                        var dayHdrSp = new StackPanel { Orientation = Orientation.Horizontal };
                        dayHdrSp.Children.Add(new TextBlock { Text = $"{dayGroup.Key:dd.MM.yyyy}", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White });
                        dayHdrSp.Children.Add(new TextBlock { Text = $"  {dayGroup.Key:dddd}", FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")), VerticalAlignment = VerticalAlignment.Bottom });
                        dayHdrSp.Children.Add(new TextBlock { Text = $"  —  {dayGroup.Count()} akcji", FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")), VerticalAlignment = VerticalAlignment.Bottom });
                        dayHdr.Child = dayHdrSp;
                        listStack.Children.Add(dayHdr);

                        var dayGrid = new System.Windows.Controls.Primitives.UniformGrid { Columns = 3 };
                        foreach (var (typ, tresc, hodowca, hodowcaId, data) in dayGroup)
                        {
                            string borderColor = typ switch { "Notatka" => "#3B82F6", "Zmiana statusu" => "#F59E0B", "Przypisanie" => "#8B5CF6", _ => "#475569" };
                            string typLabel = typ switch { "Notatka" => "Notatka", "Zmiana statusu" => "Status", "Przypisanie" => "Przypisanie", _ => typ };
                            var card = new Border
                            {
                                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(borderColor)),
                                BorderThickness = new Thickness(3, 0, 0, 0),
                                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B")),
                                CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(3, 2, 3, 2),
                                Cursor = System.Windows.Input.Cursors.Hand
                            };
                            var cardSp = new StackPanel();
                            var ln1 = new TextBlock { FontSize = 11 };
                            ln1.Inlines.Add(new System.Windows.Documents.Run($"{data:HH:mm}") { Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")), FontSize = 10 });
                            ln1.Inlines.Add(new System.Windows.Documents.Run($"  [{typLabel}]  ") { Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(borderColor)), FontWeight = FontWeights.SemiBold, FontSize = 10 });
                            ln1.Inlines.Add(new System.Windows.Documents.Run(hodowca) { Foreground = System.Windows.Media.Brushes.White, FontWeight = FontWeights.SemiBold });
                            cardSp.Children.Add(ln1);
                            if (!string.IsNullOrWhiteSpace(tresc))
                                cardSp.Children.Add(new TextBlock
                                {
                                    Text = tresc, FontSize = 10,
                                    Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")),
                                    TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0), MaxHeight = 40, TextTrimming = TextTrimming.CharacterEllipsis
                                });
                            card.Child = cardSp;
                            card.ToolTip = $"{hodowca}: {tresc}";

                            // Hover
                            card.MouseEnter += (s, e) => card.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D3B4F"));
                            card.MouseLeave += (s, e) => card.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B"));

                            // Kliknięcie → szczegóły hodowcy
                            int clickId = hodowcaId;
                            string clickName = hodowca;
                            card.MouseLeftButtonDown += (s, e) => ShowHodowcaHistory(clickId, clickName, dialog);

                            dayGrid.Children.Add(card);
                        }
                        listStack.Children.Add(dayGrid);
                    }
                }

                // === 4. Wykres porównawczy (custom WPF bars z avatarami) ===
                if (compData.Count > 0)
                {
                    var userColors = new[] { "#3B82F6", "#22C55E", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899", "#06B6D4", "#F97316" };
                    double maxVal = Math.Max(
                        compData.Max(u => (double)u.Notatki),
                        compData.Max(u => (double)u.Statusy));
                    if (maxVal == 0) maxVal = 1;
                    double maxBarH = 180;

                    var chartGrid = new Grid();
                    chartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    chartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    chartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var sep = new Border
                    {
                        Width = 1,
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                        Margin = new Thickness(8, 20, 8, 10)
                    };
                    Grid.SetColumn(sep, 1);
                    chartGrid.Children.Add(sep);

                    string[] catNames = { "Notatki / Wiadomości", "Zmiany statusu" };
                    for (int cat = 0; cat < 2; cat++)
                    {
                        var catPanel = new StackPanel();
                        catPanel.Children.Add(new TextBlock
                        {
                            Text = catNames[cat], FontSize = 13, FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10)
                        });

                        // Słupki blisko siebie — HorizontalAlignment.Center + Auto kolumny
                        var barsGrid = new Grid { Height = maxBarH + 80, HorizontalAlignment = HorizontalAlignment.Center };
                        for (int i = 0; i < compData.Count; i++)
                            barsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                        for (int i = 0; i < compData.Count; i++)
                        {
                            var u = compData[i];
                            string clr = userColors[i % userColors.Length];
                            int val = cat == 0 ? u.Notatki : u.Statusy;
                            double barH = (val / maxVal) * maxBarH;
                            if (barH < 3 && val > 0) barH = 3;
                            string uName = u.UserName?.Split(' ').FirstOrDefault() ?? u.UserId;
                            bool isSelected = u.UserId == selectedUserId;

                            var barCol = new StackPanel
                            {
                                VerticalAlignment = VerticalAlignment.Bottom,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Width = 48,
                                Cursor = System.Windows.Input.Cursors.Hand
                            };

                            // Avatar
                            var avBrd = new Border
                            {
                                Width = 26, Height = 26,
                                CornerRadius = new CornerRadius(13),
                                ClipToBounds = true,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Margin = new Thickness(0, 0, 0, 2),
                                BorderBrush = isSelected
                                    ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFF"))
                                    : null,
                                BorderThickness = isSelected ? new Thickness(2) : new Thickness(0)
                            };
                            var avIm = new System.Windows.Controls.Image { Stretch = System.Windows.Media.Stretch.UniformToFill };
                            avIm.Source = GetUserAvatar(u.UserId, u.UserName, 26);
                            avBrd.Child = avIm;
                            barCol.Children.Add(avBrd);

                            // Wartość
                            barCol.Children.Add(new TextBlock
                            {
                                Text = val.ToString(), FontSize = 10, FontWeight = FontWeights.Bold,
                                Foreground = System.Windows.Media.Brushes.White,
                                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 2)
                            });

                            // Słupek
                            var barBorder = new Border
                            {
                                Width = 22, Height = barH,
                                CornerRadius = new CornerRadius(3, 3, 0, 0),
                                Background = new System.Windows.Media.SolidColorBrush(
                                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(clr)),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Opacity = isSelected ? 1.0 : 0.7
                            };
                            barCol.Children.Add(barBorder);

                            // Imię
                            var nameTxt = new TextBlock
                            {
                                Text = uName, FontSize = 9,
                                Foreground = new System.Windows.Media.SolidColorBrush(
                                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(isSelected ? "#FFFFFF" : "#94A3B8")),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                                Margin = new Thickness(0, 2, 0, 0)
                            };
                            barCol.Children.Add(nameTxt);

                            // Zapamiętaj elementy do podświetlenia
                            barHighlightElements.Add((u.UserId, avBrd, barBorder, nameTxt));

                            // Kliknięcie → zmiana usera
                            string clickUid = u.UserId;
                            string clickUname = u.UserName;
                            barCol.MouseLeftButtonDown += async (s, e) =>
                            {
                                if (selectedUserId == clickUid) return;
                                selectedUserId = clickUid;
                                UpdateBarHighlights(clickUid);
                                await RefreshActivities(clickUid, clickUname);
                            };

                            Grid.SetColumn(barCol, i);
                            barsGrid.Children.Add(barCol);
                        }

                        catPanel.Children.Add(barsGrid);
                        Grid.SetColumn(catPanel, cat * 2);
                        chartGrid.Children.Add(catPanel);
                    }

                    var chartCard = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F172A")),
                        CornerRadius = new CornerRadius(8), Padding = new Thickness(12),
                        Child = chartGrid
                    };
                    chartBorder.Child = chartCard;

                    var infoItems = compData.Select(u =>
                    {
                        string name = u.UserName?.Split(' ').FirstOrDefault() ?? u.UserId;
                        int total = u.Notatki + u.Statusy;
                        return $"{name}: {total} ({u.UniqueHodowcy} hod.)";
                    });
                    chartInfoTxt.Text = string.Join("   |   ", infoItems);
                }

                // === 5. Załaduj aktywności wybranego usera ===
                await RefreshActivities(userId, userName);
            }
            catch (Exception ex)
            {
                statTxt.Text = $"  Błąd: {ex.Message}";
            }
        }

        private async void ShowHodowcaHistory(int hodowcaId, string hodowcaName, Window owner)
        {
            var dlg = new Window
            {
                Title = $"Historia — {hodowcaName}",
                Width = 720, Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F172A")),
                ResizeMode = ResizeMode.CanResize
            };
            WindowIconHelper.SetIcon(dlg);

            Func<string, System.Windows.Media.SolidColorBrush> br = hex =>
                new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

            var mainSp = new StackPanel { Margin = new Thickness(20) };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            scroll.Content = mainSp;
            dlg.Content = scroll;

            // Placeholder
            mainSp.Children.Add(new TextBlock { Text = "Ładowanie...", Foreground = br("#94A3B8"), FontSize = 12 });
            dlg.Show();

            try
            {
                // Load data
                string hDostawca = "", hStatus = "", hTowar = "", hMiejscowosc = "", hTel1 = "", hTel2 = "", hTel3 = "";
                string hKontrakt = "", hNotatka = "", hPrzypisanyDo = "";
                decimal hKM = 0;
                DateTime? hOstatniKontakt = null, hNastepnyKontakt = null, hDataDodania = null;

                var historia = new List<(string Typ, string Tresc, string WynikTelefonu, string StatusPrzed, string StatusPo,
                    string UserId, string UserName, DateTime Data)>();

                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();

                    // Dane hodowcy
                    using (var cmd = new SqlCommand(@"SELECT Dostawca, Status, Towar, Miejscowosc, Tel1, Tel2, Tel3,
                        KM, Kontrakt, Notatka, PrzypisanyDo, DataOstatniegoKontaktu, DataNastepnegoKontaktu, DataDodania
                        FROM Pozyskiwanie_Hodowcy WHERE Id = @id", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", hodowcaId);
                        using var r = cmd.ExecuteReader();
                        if (r.Read())
                        {
                            hDostawca = r["Dostawca"]?.ToString()?.Trim() ?? "";
                            hStatus = r["Status"]?.ToString() ?? "";
                            hTowar = r["Towar"]?.ToString() ?? "";
                            hMiejscowosc = r["Miejscowosc"]?.ToString() ?? "";
                            hTel1 = r["Tel1"]?.ToString() ?? "";
                            hTel2 = r["Tel2"]?.ToString() ?? "";
                            hTel3 = r["Tel3"]?.ToString() ?? "";
                            hKM = r["KM"] is decimal km ? km : 0;
                            hKontrakt = r["Kontrakt"]?.ToString() ?? "";
                            hNotatka = r["Notatka"]?.ToString() ?? "";
                            hPrzypisanyDo = r["PrzypisanyDo"]?.ToString() ?? "";
                            hOstatniKontakt = r["DataOstatniegoKontaktu"] is DateTime d1 ? d1 : (DateTime?)null;
                            hNastepnyKontakt = r["DataNastepnegoKontaktu"] is DateTime d2 ? d2 : (DateTime?)null;
                            hDataDodania = r["DataDodania"] is DateTime d3 ? d3 : (DateTime?)null;
                        }
                    }

                    // Historia aktywności (wszystkie, nie tylko tego usera)
                    using (var cmd2 = new SqlCommand(@"SELECT TypAktywnosci, Tresc, WynikTelefonu, StatusPrzed, StatusPo,
                        UzytkownikId, UzytkownikNazwa, DataUtworzenia
                        FROM Pozyskiwanie_Aktywnosci
                        WHERE HodowcaId = @id AND UzytkownikId != 'IMPORT'
                        ORDER BY DataUtworzenia DESC", conn))
                    {
                        cmd2.Parameters.AddWithValue("@id", hodowcaId);
                        using var r2 = cmd2.ExecuteReader();
                        while (r2.Read())
                        {
                            historia.Add((
                                r2["TypAktywnosci"]?.ToString() ?? "", r2["Tresc"]?.ToString() ?? "",
                                r2["WynikTelefonu"]?.ToString() ?? "", r2["StatusPrzed"]?.ToString() ?? "",
                                r2["StatusPo"]?.ToString() ?? "", r2["UzytkownikId"]?.ToString() ?? "",
                                r2["UzytkownikNazwa"]?.ToString() ?? "", Convert.ToDateTime(r2["DataUtworzenia"])
                            ));
                        }
                    }
                });

                mainSp.Children.Clear();

                // === KARTA HODOWCY ===
                string statusClr = hStatus switch
                {
                    "Nowy" => "#64748B", "Do zadzwonienia" => "#F59E0B", "Próba kontaktu" => "#FB923C",
                    "Nawiązano kontakt" => "#22C55E", "Zdaje" => "#10B981",
                    "Nie zainteresowany" => "#EF4444", "Obcy kontrakt" => "#06B6D4", _ => "#94A3B8"
                };

                var infoBorder = new Border
                {
                    Background = br("#1E293B"), CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(16, 14, 16, 14), Margin = new Thickness(0, 0, 0, 16)
                };
                var infoSp = new StackPanel();

                // Row 1: Status badge + name
                var row1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                var sBadge = new Border { Background = br(statusClr), CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
                sBadge.Child = new TextBlock { Text = hStatus, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = System.Windows.Media.Brushes.White };
                row1.Children.Add(sBadge);
                row1.Children.Add(new TextBlock { Text = hDostawca, FontSize = 18, FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White, VerticalAlignment = VerticalAlignment.Center });
                infoSp.Children.Add(row1);

                // Row 2: Info tags
                var row2 = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
                void AddTag(string icon, string text, string color)
                {
                    if (string.IsNullOrWhiteSpace(text)) return;
                    var tag = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 4) };
                    tag.Children.Add(new TextBlock { Text = icon, FontSize = 11, Margin = new Thickness(0, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center });
                    tag.Children.Add(new TextBlock { Text = text, FontSize = 11, Foreground = br(color), VerticalAlignment = VerticalAlignment.Center });
                    row2.Children.Add(tag);
                }
                AddTag("\uD83D\uDCE6", hTowar, "#93C5FD");
                AddTag("\uD83D\uDCCD", hMiejscowosc, "#CBD5E1");
                AddTag("\uD83D\uDE97", $"{hKM} km", "#F59E0B");
                AddTag("\uD83D\uDCDE", hTel1, "#22C55E");
                if (!string.IsNullOrWhiteSpace(hTel2)) AddTag("", hTel2, "#22C55E");
                if (!string.IsNullOrWhiteSpace(hTel3)) AddTag("", hTel3, "#22C55E");
                if (!string.IsNullOrWhiteSpace(hKontrakt)) AddTag("\uD83D\uDCC4", hKontrakt, "#A78BFA");
                if (!string.IsNullOrWhiteSpace(hPrzypisanyDo)) AddTag("\uD83D\uDC64", hPrzypisanyDo, "#60A5FA");
                infoSp.Children.Add(row2);

                // Row 3: Dates
                var row3 = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
                if (hOstatniKontakt.HasValue) AddTag("\uD83D\uDCC5", $"Ost. kontakt: {hOstatniKontakt.Value:dd.MM.yyyy}", "#94A3B8");
                if (hNastepnyKontakt.HasValue)
                {
                    string nkClr = hNastepnyKontakt.Value.Date <= DateTime.Today ? "#EF4444" : "#22C55E";
                    AddTag("\uD83D\uDCC5", $"Nast. kontakt: {hNastepnyKontakt.Value:dd.MM.yyyy}", nkClr);
                }
                if (hDataDodania.HasValue) AddTag("\u2795", $"Dodano: {hDataDodania.Value:dd.MM.yyyy}", "#64748B");
                // row3 tags are added to row2 (AddTag captures row2)
                infoSp.Children.Add(row3);

                // Notatka ogólna
                if (!string.IsNullOrWhiteSpace(hNotatka))
                {
                    var noteBorder = new Border { Background = br("#0F172A"), CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 6, 0, 0) };
                    var noteTb = new TextBlock { Text = hNotatka, FontSize = 10, Foreground = br("#CBD5E1"),
                        TextWrapping = TextWrapping.Wrap, FontStyle = FontStyles.Italic };
                    noteBorder.Child = noteTb;
                    infoSp.Children.Add(noteBorder);
                }

                infoBorder.Child = infoSp;
                mainSp.Children.Add(infoBorder);

                // === HISTORIA TIMELINE ===
                var histHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                histHeader.Children.Add(new TextBlock { Text = "HISTORIA ZMIAN I WIADOMOŚCI", FontSize = 12, FontWeight = FontWeights.Bold,
                    Foreground = br("#94A3B8") });
                histHeader.Children.Add(new TextBlock { Text = $"  ({historia.Count})", FontSize = 12, Foreground = br("#64748B") });
                mainSp.Children.Add(histHeader);

                if (historia.Count == 0)
                {
                    mainSp.Children.Add(new TextBlock { Text = "Brak historii aktywności.", FontSize = 11, Foreground = br("#64748B"),
                        Margin = new Thickness(20, 10, 0, 0) });
                }

                foreach (var dayGroup in historia.GroupBy(h => h.Data.Date).OrderByDescending(g => g.Key))
                {
                    // Day header
                    var dayHdr = new TextBlock { FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = br("#64748B"),
                        Margin = new Thickness(0, 12, 0, 6), Text = $"{dayGroup.Key:dd.MM.yyyy} — {dayGroup.Key:dddd}" };
                    mainSp.Children.Add(dayHdr);

                    foreach (var entry in dayGroup.OrderByDescending(e => e.Data))
                    {
                        string typeColor = entry.Typ switch
                        {
                            "Notatka" => "#3B82F6", "Zmiana statusu" => "#F59E0B",
                            "Przypisanie" => "#8B5CF6", "Telefon" => "#22C55E", _ => "#475569"
                        };
                        string typeIcon = entry.Typ switch
                        {
                            "Notatka" => "\uD83D\uDCDD", "Zmiana statusu" => "\uD83D\uDD04",
                            "Przypisanie" => "\uD83D\uDC64", "Telefon" => "\uD83D\uDCDE", _ => "\uD83D\uDCCB"
                        };

                        // Karta timeline: Grid z 2 kolumnami [timeline dot | content]
                        var entryGrid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                        entryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                        entryGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        // Kolumna 0: linia + kropka
                        var dotPanel = new Grid();
                        var line = new Border { Width = 2, Background = br("#334155"), HorizontalAlignment = HorizontalAlignment.Center };
                        dotPanel.Children.Add(line);
                        var dot = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5),
                            Background = br(typeColor), HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 14, 0, 0) };
                        dotPanel.Children.Add(dot);
                        Grid.SetColumn(dotPanel, 0);
                        entryGrid.Children.Add(dotPanel);

                        // Kolumna 1: karta z treścią
                        var cardBorder = new Border
                        {
                            Background = br("#1E293B"), CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 2, 0, 2)
                        };
                        var cardSp = new StackPanel();

                        // Wiersz 1: Avatar + UserName + Czas
                        var userRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                        var avBrd = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11),
                            ClipToBounds = true, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                        var avIm = new System.Windows.Controls.Image { Stretch = System.Windows.Media.Stretch.UniformToFill };
                        avIm.Source = GetUserAvatar(entry.UserId, entry.UserName, 22);
                        avBrd.Child = avIm;
                        userRow.Children.Add(avBrd);
                        userRow.Children.Add(new TextBlock { Text = entry.UserName, FontSize = 11, FontWeight = FontWeights.SemiBold,
                            Foreground = br("#60A5FA"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                        userRow.Children.Add(new TextBlock { Text = $"{entry.Data:HH:mm}", FontSize = 10,
                            Foreground = br("#64748B"), VerticalAlignment = VerticalAlignment.Center });
                        cardSp.Children.Add(userRow);

                        // Wiersz 2: Typ badge
                        var typRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 2) };
                        typRow.Children.Add(new TextBlock { Text = typeIcon, FontSize = 11, Margin = new Thickness(0, 0, 4, 0) });
                        var typBadge = new Border { Background = br(typeColor), CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(5, 1, 5, 1), Opacity = 0.85 };
                        typBadge.Child = new TextBlock { Text = entry.Typ, FontSize = 9, FontWeight = FontWeights.Bold,
                            Foreground = System.Windows.Media.Brushes.White };
                        typRow.Children.Add(typBadge);

                        // Dla zmian statusu — pokaż StatusPrzed → StatusPo
                        if (entry.Typ == "Zmiana statusu" && !string.IsNullOrWhiteSpace(entry.StatusPrzed))
                        {
                            typRow.Children.Add(new TextBlock { Text = $"  {entry.StatusPrzed} \u2192 {entry.StatusPo}",
                                FontSize = 11, Foreground = br("#CBD5E1"), VerticalAlignment = VerticalAlignment.Center,
                                FontWeight = FontWeights.SemiBold });
                        }
                        // Dla telefonów — wynik
                        if (entry.Typ == "Telefon" && !string.IsNullOrWhiteSpace(entry.WynikTelefonu))
                        {
                            typRow.Children.Add(new TextBlock { Text = $"  ({entry.WynikTelefonu})",
                                FontSize = 10, Foreground = br("#94A3B8"), VerticalAlignment = VerticalAlignment.Center });
                        }
                        cardSp.Children.Add(typRow);

                        // Wiersz 3: Treść
                        if (!string.IsNullOrWhiteSpace(entry.Tresc))
                        {
                            cardSp.Children.Add(new TextBlock { Text = entry.Tresc, FontSize = 10, Foreground = br("#CBD5E1"),
                                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 0) });
                        }

                        cardBorder.Child = cardSp;
                        Grid.SetColumn(cardBorder, 1);
                        entryGrid.Children.Add(cardBorder);

                        mainSp.Children.Add(entryGrid);
                    }
                }
            }
            catch (Exception ex)
            {
                mainSp.Children.Clear();
                mainSp.Children.Add(new TextBlock { Text = $"Błąd: {ex.Message}", Foreground = System.Windows.Media.Brushes.Red, FontSize = 12 });
            }
        }

        #endregion

        #region Avatar

        private void DgHodowcy_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row.Item is DataRowView drv)
            {
                string ostatniUserId = drv["OstatniUserId"]?.ToString();
                string ostatniUserNazwa = drv["OstatniUserNazwa"]?.ToString();

                // Zawsze ustaw avatar - wiersz może być recyklowany z innym avatarem
                try
                {
                    var avatarImage = FindVisualChild<System.Windows.Controls.Image>(e.Row, "imgAvatar");
                    if (avatarImage != null)
                    {
                        avatarImage.Source = string.IsNullOrWhiteSpace(ostatniUserId)
                            ? null
                            : GetUserAvatar(ostatniUserId, ostatniUserNazwa, 22);
                    }
                }
                catch { }

                // Fallback: jeśli visual tree nie jest jeszcze gotowe, ustaw po załadowaniu
                // Używamy Tag aby nie dodawać handlera wielokrotnie
                if (e.Row.Tag as string != "avatarHooked")
                {
                    e.Row.Tag = "avatarHooked";
                    e.Row.Loaded += (s, args) =>
                    {
                        try
                        {
                            if (((DataGridRow)s).Item is DataRowView rowDrv)
                            {
                                string uid = rowDrv["OstatniUserId"]?.ToString();
                                string uname = rowDrv["OstatniUserNazwa"]?.ToString();
                                var img = FindVisualChild<System.Windows.Controls.Image>((DataGridRow)s, "imgAvatar");
                                if (img != null)
                                {
                                    img.Source = string.IsNullOrWhiteSpace(uid)
                                        ? null
                                        : GetUserAvatar(uid, uname, 22);
                                }
                            }
                        }
                        catch { }
                    };
                }
            }
        }

        private BitmapSource GetUserAvatar(string userId, string userName, int size)
        {
            if (string.IsNullOrWhiteSpace(userId)) return null;

            string cacheKey = $"{userId}_{size}";
            if (_avatarCache.TryGetValue(cacheKey, out var cached))
                return cached;

            BitmapSource source = null;
            try
            {
                System.Drawing.Image img = null;
                if (UserAvatarManager.HasAvatar(userId))
                    img = UserAvatarManager.GetAvatarRounded(userId, size);
                if (img == null)
                    img = UserAvatarManager.GenerateDefaultAvatar(userName ?? userId, userId, size);

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

            _avatarCache[cacheKey] = source;
            return source;
        }

        private static T FindVisualChild<T>(DependencyObject parent, string name = null) where T : FrameworkElement
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && (name == null || typedChild.Name == name))
                    return typedChild;

                var found = FindVisualChild<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }

        #endregion

        #region Selection & Details

        private void DgHodowcy_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgHodowcy.SelectedItem is DataRowView drv)
            {
                aktualnyHodowcaId = Convert.ToInt32(drv["Id"]);
                panelEmptyState.Visibility = Visibility.Collapsed;
                scrollDetails.Visibility = Visibility.Visible;
                ShowDetails(drv);
                LoadAktywnosci(aktualnyHodowcaId);

                // Szablon rozmowy
                UstawSzablonRozmowy();

                // Auto-search delivery history — first 5 chars of first word
                string nazwa = drv["Dostawca"]?.ToString() ?? "";
                var firstWord = nazwa.Split(new[] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
                txtSzukajDostawy.Text = firstWord.Length > 5 ? firstWord.Substring(0, 5) : firstWord;
                SzukajDostawy();
            }
        }

        private void ShowDetails(DataRowView drv)
        {
            txtDetNazwa.Text = drv["Dostawca"]?.ToString();

            // Towar badge
            string towar = drv["Towar"]?.ToString();
            txtDetTowar.Text = towar;
            badgeDetTowar.Visibility = !string.IsNullOrEmpty(towar) ? Visibility.Visible : Visibility.Collapsed;
            if (!string.IsNullOrEmpty(towar))
            {
                badgeDetTowar.Background = towar switch
                {
                    "KURCZAKI" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#78350F")),
                    "GESI" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14532D")),
                    "KACZKI" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E3A8A")),
                    "DROB" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#312E81")),
                    "PERLICZKI" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#581C87")),
                    _ => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155"))
                };
            }

            // Status badge
            string status = drv["Status"]?.ToString();
            txtDetStatus.Text = status;
            badgeDetStatus.Background = status switch
            {
                "Do zadzwonienia" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#78350F")),
                "Próba kontaktu" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#78350F")),
                "Nawiązano kontakt" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14532D")),
                "Zdaje" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14532D")),
                "Nie zainteresowany" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7F1D1D")),
                "Obcy kontrakt" => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#134E4A")),
                _ => new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#374151"))
            };

            // Address
            string adres = "";
            string ulica = drv["Ulica"]?.ToString();
            string kod = drv["KodPocztowy"]?.ToString();
            string miasto = drv["Miejscowosc"]?.ToString();
            if (!string.IsNullOrEmpty(ulica)) adres += ulica;
            if (!string.IsNullOrEmpty(kod) || !string.IsNullOrEmpty(miasto))
                adres += (adres.Length > 0 ? ", " : "") + $"{kod} {miasto}".Trim();
            txtDetAdres.Text = adres;

            // KM
            var km = drv["KM"];
            if (km != DBNull.Value)
            {
                txtDetKm.Text = $"{km} km";
                panelDetKm.Visibility = Visibility.Visible;
            }
            else
            {
                panelDetKm.Visibility = Visibility.Collapsed;
            }

            // Województwo + Powiat
            string wojewodztwo = drv["Wojewodztwo"]?.ToString();
            string powiatVal = drv["Powiat"]?.ToString();
            if (!string.IsNullOrEmpty(wojewodztwo))
            {
                txtDetWoj.Text = !string.IsNullOrEmpty(powiatVal)
                    ? $"{wojewodztwo}, pow. {powiatVal}"
                    : wojewodztwo;
                panelDetWoj.Visibility = Visibility.Visible;
            }
            else
            {
                panelDetWoj.Visibility = Visibility.Collapsed;
            }

            // Notatka z Excela
            string notatka = drv["Notatka"]?.ToString();
            if (!string.IsNullOrEmpty(notatka))
            {
                txtDetNotatka.Text = notatka;
                panelDetNotatka.Visibility = Visibility.Visible;
            }
            else
            {
                panelDetNotatka.Visibility = Visibility.Collapsed;
            }

            // Telefony
            string tel1 = drv["Tel1"]?.ToString();
            string tel2 = drv["Tel2"]?.ToString();
            string tel3 = drv["Tel3"]?.ToString();

            bool hasTel = !string.IsNullOrEmpty(tel1) || !string.IsNullOrEmpty(tel2) || !string.IsNullOrEmpty(tel3);
            txtBrakTelefonow.Visibility = hasTel ? Visibility.Collapsed : Visibility.Visible;

            panelTel1.Visibility = !string.IsNullOrEmpty(tel1) ? Visibility.Visible : Visibility.Collapsed;
            txtDetTel1.Text = FormatPhone(tel1);
            panelTel2.Visibility = !string.IsNullOrEmpty(tel2) ? Visibility.Visible : Visibility.Collapsed;
            txtDetTel2.Text = FormatPhone(tel2);
            panelTel3.Visibility = !string.IsNullOrEmpty(tel3) ? Visibility.Visible : Visibility.Collapsed;
            txtDetTel3.Text = FormatPhone(tel3);

        }

        private string FormatPhone(string phone)
        {
            if (string.IsNullOrEmpty(phone)) return "";
            string digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length == 9)
                return $"{digits.Substring(0, 3)} {digits.Substring(3, 3)} {digits.Substring(6, 3)}";
            if (digits.Length == 11 && digits.StartsWith("48"))
                return $"{digits.Substring(2, 3)} {digits.Substring(5, 3)} {digits.Substring(8, 3)}";
            return phone;
        }

        private List<string> SplitPhoneNumbers(string raw)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return result;

            // Split by comma, semicolon, slash
            var parts = raw.Split(new[] { ',', ';', '/' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                string digits = new string(part.Where(char.IsDigit).ToArray());

                if (digits.Length == 18)
                {
                    // Two 9-digit numbers concatenated
                    result.Add(digits.Substring(0, 9));
                    result.Add(digits.Substring(9, 9));
                }
                else if (digits.Length == 20 && digits.StartsWith("48"))
                {
                    // Two numbers with +48 prefix concatenated: 48XXXXXXXXX48XXXXXXXXX
                    result.Add(digits.Substring(2, 9));
                    result.Add(digits.Substring(13, 9));
                }
                else if (digits.Length >= 7)
                {
                    result.Add(digits);
                }
            }

            return result;
        }

        #endregion

        #region Aktywności (History)

        private async void LoadAktywnosci(int hodowcaId)
        {
            try
            {
                var aktywnosci = new List<AktywnoscModel>();
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();
                    string sql = @"SELECT Id, TypAktywnosci, Tresc, WynikTelefonu, StatusPrzed, StatusPo,
                                          UzytkownikId, UzytkownikNazwa, DataUtworzenia
                                   FROM Pozyskiwanie_Aktywnosci
                                   WHERE HodowcaId = @id
                                   ORDER BY DataUtworzenia DESC";
                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@id", hodowcaId);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        aktywnosci.Add(new AktywnoscModel
                        {
                            Id = Convert.ToInt32(reader["Id"]),
                            TypAktywnosci = reader["TypAktywnosci"]?.ToString(),
                            Tresc = reader["Tresc"]?.ToString(),
                            WynikTelefonu = reader["WynikTelefonu"] is DBNull ? null : reader["WynikTelefonu"]?.ToString(),
                            StatusPrzed = reader["StatusPrzed"] is DBNull ? null : reader["StatusPrzed"]?.ToString(),
                            StatusPo = reader["StatusPo"] is DBNull ? null : reader["StatusPo"]?.ToString(),
                            UzytkownikId = reader["UzytkownikId"]?.ToString() == "IMPORT" ? "" : reader["UzytkownikId"]?.ToString(),
                            UzytkownikNazwa = reader["UzytkownikId"]?.ToString() == "IMPORT" ? "" : reader["UzytkownikNazwa"]?.ToString(),
                            DataUtworzenia = Convert.ToDateTime(reader["DataUtworzenia"])
                        });
                    }
                });

                listaAktywnosci.ItemsSource = aktywnosci;

                // Load avatars for history items
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        listaAktywnosci.UpdateLayout();
                        for (int i = 0; i < aktywnosci.Count; i++)
                        {
                            var container = listaAktywnosci.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                            if (container == null) continue;
                            var img = FindVisualChild<System.Windows.Controls.Image>(container, "imgAktywnoscAvatar");
                            if (img != null && img.Source == null)
                            {
                                img.Source = GetUserAvatar(aktywnosci[i].UzytkownikId, aktywnosci[i].UzytkownikNazwa, 24);
                            }
                        }
                    }
                    catch { }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LoadAktywnosci error: {ex.Message}");
            }
        }

        #endregion

        #region Status Changes

        private void BtnQuickStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string newStatus)
                ChangeStatus(newStatus);
        }

        private void MenuZmienStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.Tag is string newStatus)
                ChangeStatus(newStatus);
        }

        private async void ChangeStatus(string newStatus)
        {
            if (aktualnyHodowcaId == 0) return;
            if (dgHodowcy.SelectedItem is not DataRowView drv) return;

            string oldStatus = drv["Status"]?.ToString();
            if (oldStatus == newStatus) return;

            try
            {
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();

                    // Update status
                    using var cmd = new SqlCommand("UPDATE Pozyskiwanie_Hodowcy SET Status = @status WHERE Id = @id", conn);
                    cmd.Parameters.AddWithValue("@status", newStatus);
                    cmd.Parameters.AddWithValue("@id", aktualnyHodowcaId);
                    cmd.ExecuteNonQuery();

                    // Log activity
                    string tresc = $"Zmiana statusu: {oldStatus} → {newStatus}";
                    InsertAktywnosc(conn, aktualnyHodowcaId, "Zmiana statusu", tresc, null, oldStatus, newStatus);
                });

                drv["Status"] = newStatus;
                string tresc = $"Zmiana statusu: {oldStatus} → {newStatus}";
                UpdateOstatniaAktywnosc(drv, tresc);

                LoadAktywnosci(aktualnyHodowcaId);
                ShowDetails(drv);
                ShowToast($"Status zmieniony na: {newStatus}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zmiany statusu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Notes

        private void TxtNowaNotatka_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                DodajNotatke();
        }

        private void BtnDodajNotatke_Click(object sender, RoutedEventArgs e)
        {
            DodajNotatke();
        }

        private async void DodajNotatke()
        {
            if (aktualnyHodowcaId == 0) return;
            string tresc = txtNowaNotatka.Text?.Trim();
            if (string.IsNullOrEmpty(tresc)) return;

            try
            {
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();
                    InsertAktywnosc(conn, aktualnyHodowcaId, "Notatka", tresc, null, null, null);
                });

                txtNowaNotatka.Text = "";
                if (dgHodowcy.SelectedItem is DataRowView drvNote)
                    UpdateOstatniaAktywnosc(drvNote, tresc);
                LoadAktywnosci(aktualnyHodowcaId);
                ShowToast("Notatka dodana");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd dodawania notatki:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Call (Zadzwoń)

        private void BtnZadzwon_Click(object sender, RoutedEventArgs e)
        {
            RejestrujTelefon();
        }

        private void MenuZadzwon_Click(object sender, RoutedEventArgs e)
        {
            RejestrujTelefon();
        }

        private async void RejestrujTelefon()
        {
            if (aktualnyHodowcaId == 0) return;
            if (dgHodowcy.SelectedItem is not DataRowView drv) return;

            // Dialog wyniku rozmowy — dark theme
            var dialog = new Window
            {
                Title = "Wynik rozmowy telefonicznej",
                Width = 520,
                Height = 620,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F172A")),
                ResizeMode = ResizeMode.NoResize
            };
            WindowIconHelper.SetIcon(dialog);

            var outerScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var outerBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B")),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(12),
                Padding = new Thickness(24)
            };

            var stack = new StackPanel();

            // Header with phone icon
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            var headerIcon = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14532D")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 10, 0)
            };
            headerIcon.Child = new TextBlock { Text = "\U0001F4DE", FontSize = 18 };
            header.Children.Add(headerIcon);

            var headerText = new StackPanel();
            headerText.Children.Add(new TextBlock { Text = drv["Dostawca"]?.ToString(), FontSize = 15, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0")) });
            headerText.Children.Add(new TextBlock { Text = "Rejestracja rozmowy telefonicznej", FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")) });
            header.Children.Add(headerText);
            stack.Children.Add(header);

            // --- Szablon rozmowy ---
            string userName = App.UserFullName ?? App.UserID ?? "handlowiec";
            string szablon = _szablonyRozmow[_rng.Next(_szablonyRozmow.Length)].Replace("{nazwa}", userName);

            var scriptLabel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            scriptLabel.Children.Add(new TextBlock { Text = "\U0001F4AC", FontSize = 11, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center });
            scriptLabel.Children.Add(new TextBlock { Text = "PODPOWIEDŹ — CO POWIEDZIEĆ", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")), VerticalAlignment = VerticalAlignment.Center });
            stack.Children.Add(scriptLabel);

            var scriptBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F2E1A")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 14),
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E")),
                BorderThickness = new Thickness(1, 0, 0, 0)
            };
            var scriptStack = new StackPanel();
            var scriptText = new TextBlock
            {
                Text = szablon,
                FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4ADE80")),
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyles.Italic,
                LineHeight = 18
            };
            scriptStack.Children.Add(scriptText);

            var btnNextScript = new Button
            {
                Content = "\U0001F504  Inny szablon",
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(10, 4, 10, 4),
                FontSize = 10,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnNextScript.Click += (s, args) =>
            {
                scriptText.Text = _szablonyRozmow[_rng.Next(_szablonyRozmow.Length)].Replace("{nazwa}", userName);
            };
            scriptStack.Children.Add(btnNextScript);

            scriptBorder.Child = scriptStack;
            stack.Children.Add(scriptBorder);

            var label = new TextBlock { Text = "WYNIK ROZMOWY", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")), Margin = new Thickness(0, 0, 0, 6) };
            stack.Children.Add(label);

            var combo = new ComboBox
            {
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 16),
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0")),
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#475569")),
                Padding = new Thickness(8, 6, 8, 6)
            };
            combo.Items.Add("Nie odebrano");
            combo.Items.Add("Zajęte");
            combo.Items.Add("Rozmowa");
            combo.Items.Add("Umówiono kontakt");
            combo.Items.Add("Odmowa");
            combo.SelectedIndex = 0;
            stack.Children.Add(combo);

            var noteLabel = new TextBlock { Text = "NOTATKA Z ROZMOWY", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")), Margin = new Thickness(0, 0, 0, 6) };
            stack.Children.Add(noteLabel);

            var noteBox = new TextBox
            {
                Height = 90,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                FontSize = 12,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F172A")),
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0")),
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                CaretBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0")),
                Padding = new Thickness(10, 8, 10, 8)
            };
            stack.Children.Add(noteBox);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };

            var btnCancel = new Button
            {
                Content = "Anuluj",
                Padding = new Thickness(18, 8, 18, 8),
                FontSize = 12,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, args) => { dialog.DialogResult = false; };
            btnPanel.Children.Add(btnCancel);

            var btnOk = new Button
            {
                Content = "\U0001F4BE  Zapisz rozmowę",
                Padding = new Thickness(20, 8, 20, 8),
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E")),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            btnOk.Click += (s, args) => { dialog.DialogResult = true; };
            btnPanel.Children.Add(btnOk);

            stack.Children.Add(btnPanel);

            outerBorder.Child = stack;
            outerScroll.Content = outerBorder;
            dialog.Content = outerScroll;

            if (dialog.ShowDialog() == true)
            {
                string wynik = combo.SelectedItem?.ToString();
                string notatka = noteBox.Text?.Trim();
                string tresc = $"Telefon — wynik: {wynik}";
                if (!string.IsNullOrEmpty(notatka))
                    tresc += $"\n{notatka}";

                try
                {
                    await Task.Run(() =>
                    {
                        using var conn = new SqlConnection(connectionString);
                        conn.Open();

                        // Log call
                        InsertAktywnosc(conn, aktualnyHodowcaId, "Telefon", tresc, wynik, null, null);

                        // Update last contact date
                        using var cmd = new SqlCommand("UPDATE Pozyskiwanie_Hodowcy SET DataOstatniegoKontaktu = GETDATE() WHERE Id = @id", conn);
                        cmd.Parameters.AddWithValue("@id", aktualnyHodowcaId);
                        cmd.ExecuteNonQuery();

                        // Auto-change status if "Nowy" or "Do zadzwonienia"
                        string currentStatus = drv["Status"]?.ToString();
                        if (currentStatus == "Nowy" || currentStatus == "Do zadzwonienia")
                        {
                            string autoStatus = wynik == "Nie odebrano" || wynik == "Zajęte" ? "Próba kontaktu" :
                                                wynik == "Rozmowa" || wynik == "Umówiono kontakt" ? "Nawiązano kontakt" :
                                                wynik == "Odmowa" ? "Nie zainteresowany" : currentStatus;

                            if (autoStatus != currentStatus)
                            {
                                using var cmd2 = new SqlCommand("UPDATE Pozyskiwanie_Hodowcy SET Status = @status WHERE Id = @id", conn);
                                cmd2.Parameters.AddWithValue("@status", autoStatus);
                                cmd2.Parameters.AddWithValue("@id", aktualnyHodowcaId);
                                cmd2.ExecuteNonQuery();

                                InsertAktywnosc(conn, aktualnyHodowcaId, "Zmiana statusu", $"Auto: {currentStatus} → {autoStatus}", null, currentStatus, autoStatus);

                                Dispatcher.Invoke(() => drv["Status"] = autoStatus);
                            }
                        }
                    });

                    drv["DataOstatniegoKontaktu"] = DateTime.Now;
                    UpdateOstatniaAktywnosc(drv, tresc);
    
                    LoadAktywnosci(aktualnyHodowcaId);
                    ShowDetails(drv);
                    ShowToast($"Telefon zarejestrowany: {wynik}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd rejestracji telefonu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TxtTelefon_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBlock tb && !string.IsNullOrEmpty(tb.Text))
            {
                string digits = new string(tb.Text.Where(char.IsDigit).ToArray());
                Clipboard.SetText(digits);
                ShowToast($"Skopiowano: {digits}");
            }
        }

        #endregion

        #region Assign (Przypisz)

        private void BtnPrzypisz_Click(object sender, RoutedEventArgs e)
        {
            PrzypiszDoMnie();
        }

        private void MenuPrzypisz_Click(object sender, RoutedEventArgs e)
        {
            PrzypiszDoMnie();
        }

        private async void PrzypiszDoMnie()
        {
            if (aktualnyHodowcaId == 0) return;
            if (dgHodowcy.SelectedItem is not DataRowView drv) return;

            try
            {
                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();

                    using var cmd = new SqlCommand("UPDATE Pozyskiwanie_Hodowcy SET PrzypisanyDo = @userId WHERE Id = @id", conn);
                    cmd.Parameters.AddWithValue("@userId", App.UserID);
                    cmd.Parameters.AddWithValue("@id", aktualnyHodowcaId);
                    cmd.ExecuteNonQuery();

                    InsertAktywnosc(conn, aktualnyHodowcaId, "Przypisanie", $"Przypisano do: {App.UserFullName}", null, null, null);
                });

                drv["PrzypisanyDo"] = App.UserID;
                drv["PrzypisanyNazwa"] = App.UserFullName;
                UpdateOstatniaAktywnosc(drv, $"Przypisano do: {App.UserFullName}");
                ShowDetails(drv);
                ShowToast($"Przypisano do: {App.UserFullName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd przypisywania:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        private void UstawSzablonRozmowy()
        {
            string userName = App.UserFullName ?? App.UserID ?? "handlowiec";
            txtSzablonRozmowy.Text = _szablonyRozmow[_rng.Next(_szablonyRozmow.Length)].Replace("{nazwa}", userName);
        }

        private void BtnNastepnySzablon_Click(object sender, RoutedEventArgs e)
        {
            UstawSzablonRozmowy();
        }

        private void BtnTogglePodpowiedz_Click(object sender, RoutedEventArgs e)
        {
            if (txtSzablonRozmowy.Visibility == Visibility.Visible)
            {
                txtSzablonRozmowy.Visibility = Visibility.Collapsed;
                txtTogglePodpowiedz.Text = "\u25BC"; // ▼
            }
            else
            {
                txtSzablonRozmowy.Visibility = Visibility.Visible;
                txtTogglePodpowiedz.Text = "\u25B2"; // ▲
            }
        }

        #region Ranking

        private void UserAvatar_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                ShowRanking();
        }

        private async void ShowRanking()
        {
            var dialog = new Window
            {
                Title = "Ranking aktywności",
                Width = 750,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F172A")),
                ResizeMode = ResizeMode.CanResize
            };
            WindowIconHelper.SetIcon(dialog);

            var mainGrid = new Grid { Margin = new Thickness(16) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // --- Header ---
            var headerBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B")),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18, 12, 18, 12),
                Margin = new Thickness(0, 0, 0, 12),
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(1)
            };
            var headerSp = new StackPanel { Orientation = Orientation.Horizontal };
            headerSp.Children.Add(new TextBlock { Text = "\U0001F3C6", FontSize = 22, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
            var headerTexts = new StackPanel();
            headerTexts.Children.Add(new TextBlock { Text = "Ranking aktywności handlowców", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0")) });
            headerTexts.Children.Add(new TextBlock { Text = "1 pkt = notatka, telefon lub zmiana statusu. Kliknij handlowca aby zobaczyć szczegóły.", FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")) });
            headerSp.Children.Add(headerTexts);
            headerBorder.Child = headerSp;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // --- Content: Left = ranking, Right = details ---
            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(contentGrid, 1);
            mainGrid.Children.Add(contentGrid);

            // Left: ranking list
            var rankBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B")),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 10, 0),
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(1)
            };
            var rankStack = new StackPanel();
            var rankScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var rankList = new StackPanel();
            rankScroll.Content = rankList;
            rankStack.Children.Add(rankScroll);
            rankBorder.Child = rankStack;
            Grid.SetColumn(rankBorder, 0);
            contentGrid.Children.Add(rankBorder);

            // Right: details
            var detBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B")),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(1)
            };
            var detHeader = new TextBlock
            {
                Text = "Kliknij handlowca z listy...",
                FontSize = 13,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var detScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var detList = new StackPanel();
            detScroll.Content = detList;
            var detStack = new StackPanel();
            detStack.Children.Add(detHeader);
            detStack.Children.Add(detScroll);
            detBorder.Child = detStack;
            Grid.SetColumn(detBorder, 1);
            contentGrid.Children.Add(detBorder);

            dialog.Content = mainGrid;
            dialog.Show();

            // --- Load ranking data ---
            try
            {
                var ranking = new List<(string UserId, string UserName, int Dzis, int Tydzien)>();

                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();

                    string sql = @"SELECT UzytkownikId, UzytkownikNazwa,
                                          SUM(CASE WHEN CONVERT(date, DataUtworzenia) = CONVERT(date, GETDATE()) THEN 1 ELSE 0 END) AS Dzis,
                                          SUM(CASE WHEN DataUtworzenia >= DATEADD(day, 1-DATEPART(weekday, GETDATE()), CONVERT(date, GETDATE())) THEN 1 ELSE 0 END) AS Tydzien
                                   FROM Pozyskiwanie_Aktywnosci
                                   WHERE TypAktywnosci IN ('Notatka','Telefon','Zmiana statusu')
                                     AND UzytkownikId != 'IMPORT'
                                   GROUP BY UzytkownikId, UzytkownikNazwa
                                   ORDER BY SUM(CASE WHEN CONVERT(date, DataUtworzenia) = CONVERT(date, GETDATE()) THEN 1 ELSE 0 END) DESC,
                                            SUM(CASE WHEN DataUtworzenia >= DATEADD(day, 1-DATEPART(weekday, GETDATE()), CONVERT(date, GETDATE())) THEN 1 ELSE 0 END) DESC";

                    using var cmd = new SqlCommand(sql, conn);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        ranking.Add((
                            reader["UzytkownikId"]?.ToString()?.Trim() ?? "",
                            reader["UzytkownikNazwa"]?.ToString()?.Trim() ?? "",
                            Convert.ToInt32(reader["Dzis"]),
                            Convert.ToInt32(reader["Tydzien"])
                        ));
                    }
                });

                // Column headers
                var colHeader = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                colHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                colHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                colHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                colHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });
                AddTextToGrid(colHeader, "#", 0, 9, "#64748B", FontWeights.Bold);
                AddTextToGrid(colHeader, "Handlowiec", 1, 9, "#64748B", FontWeights.Bold);
                AddTextToGrid(colHeader, "Dziś", 2, 9, "#64748B", FontWeights.Bold, HorizontalAlignment.Center);
                AddTextToGrid(colHeader, "Tydzień", 3, 9, "#64748B", FontWeights.Bold, HorizontalAlignment.Center);
                rankList.Children.Add(colHeader);

                // Separator
                rankList.Children.Add(new Border { Height = 1, Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")), Margin = new Thickness(0, 0, 0, 4) });

                for (int i = 0; i < ranking.Count; i++)
                {
                    var (userId, userName, dzis, tydzien) = ranking[i];
                    int nr = i + 1;
                    string medal = nr == 1 ? "\U0001F947" : nr == 2 ? "\U0001F948" : nr == 3 ? "\U0001F949" : $"{nr}.";
                    string bgColor = nr % 2 == 0 ? "#273548" : "#1E293B";
                    bool isCurrentUser = userId == App.UserID;

                    var rowBorder = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bgColor)),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(6, 6, 6, 6),
                        Margin = new Thickness(0, 0, 0, 2),
                        Cursor = Cursors.Hand,
                        BorderBrush = isCurrentUser
                            ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E"))
                            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent),
                        BorderThickness = new Thickness(isCurrentUser ? 1 : 0)
                    };

                    var rowGrid = new Grid();
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(55) });

                    // Medal/number
                    AddTextToGrid(rowGrid, medal, 0, 13, "#E2E8F0", FontWeights.Bold);

                    // Avatar
                    var avatarBorder = new Border
                    {
                        Width = 24, Height = 24, CornerRadius = new CornerRadius(12),
                        ClipToBounds = true, Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#312E81")),
                        Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center
                    };
                    var avatarImg = new System.Windows.Controls.Image { Stretch = System.Windows.Media.Stretch.UniformToFill };
                    avatarImg.Source = GetUserAvatar(userId, userName, 24);
                    avatarBorder.Child = avatarImg;
                    Grid.SetColumn(avatarBorder, 1);
                    rowGrid.Children.Add(avatarBorder);

                    // Name
                    string nameColor = isCurrentUser ? "#22C55E" : "#E2E8F0";
                    AddTextToGrid(rowGrid, userName, 2, 12, nameColor, isCurrentUser ? FontWeights.Bold : FontWeights.SemiBold);

                    // Today points
                    var dzisBlock = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dzis > 0 ? "#14532D" : "#334155")),
                        CornerRadius = new CornerRadius(4), Padding = new Thickness(4, 2, 4, 2),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                    };
                    dzisBlock.Child = new TextBlock { Text = dzis.ToString(), FontSize = 13, FontWeight = FontWeights.Black, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(dzis > 0 ? "#22C55E" : "#64748B")), HorizontalAlignment = HorizontalAlignment.Center };
                    Grid.SetColumn(dzisBlock, 3);
                    rowGrid.Children.Add(dzisBlock);

                    // Week points
                    var tydBlock = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tydzien > 0 ? "#1E3A5F" : "#334155")),
                        CornerRadius = new CornerRadius(4), Padding = new Thickness(4, 2, 4, 2),
                        HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                    };
                    tydBlock.Child = new TextBlock { Text = tydzien.ToString(), FontSize = 13, FontWeight = FontWeights.Black, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tydzien > 0 ? "#60A5FA" : "#64748B")), HorizontalAlignment = HorizontalAlignment.Center };
                    Grid.SetColumn(tydBlock, 4);
                    rowGrid.Children.Add(tydBlock);

                    rowBorder.Child = rowGrid;

                    // Click → load details
                    string capturedUserId = userId;
                    string capturedUserName = userName;
                    rowBorder.MouseLeftButtonDown += async (s, args) =>
                    {
                        detHeader.Text = $"\U0001F4CB Aktywności: {capturedUserName} (ostatnie 7 dni)";
                        detHeader.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0"));
                        detList.Children.Clear();

                        // Highlight selected row
                        foreach (var child in rankList.Children)
                        {
                            if (child is Border b && b != rowBorder)
                            {
                                b.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent);
                                b.BorderThickness = new Thickness(0);
                            }
                        }
                        rowBorder.BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E"));
                        rowBorder.BorderThickness = new Thickness(1);

                        await LoadUserDetails(capturedUserId, detList);
                    };

                    rankList.Children.Add(rowBorder);
                }
            }
            catch (Exception ex)
            {
                rankList.Children.Add(new TextBlock { Text = $"Błąd: {ex.Message}", Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FCA5A5")), FontSize = 11 });
            }
        }

        private async Task LoadUserDetails(string userId, StackPanel detList)
        {
            try
            {
                var details = new List<(string Typ, string Tresc, string Hodowca, DateTime Data)>();

                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();

                    string sql = @"SELECT a.TypAktywnosci, a.Tresc, h.Dostawca, a.DataUtworzenia
                                   FROM Pozyskiwanie_Aktywnosci a
                                   JOIN Pozyskiwanie_Hodowcy h ON a.HodowcaId = h.Id
                                   WHERE a.UzytkownikId = @userId
                                     AND a.DataUtworzenia >= DATEADD(day, -7, GETDATE())
                                     AND a.UzytkownikId != 'IMPORT'
                                   ORDER BY a.DataUtworzenia DESC";

                    using var cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        details.Add((
                            reader["TypAktywnosci"]?.ToString() ?? "",
                            reader["Tresc"]?.ToString() ?? "",
                            reader["Dostawca"]?.ToString()?.Trim() ?? "",
                            Convert.ToDateTime(reader["DataUtworzenia"])
                        ));
                    }
                });

                detList.Children.Clear();

                if (details.Count == 0)
                {
                    detList.Children.Add(new TextBlock
                    {
                        Text = "Brak aktywności w ostatnich 7 dniach",
                        FontSize = 12, FontStyle = FontStyles.Italic,
                        Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B"))
                    });
                    return;
                }

                // Group by day
                var grouped = details.GroupBy(d => d.Data.Date).OrderByDescending(g => g.Key);

                foreach (var dayGroup in grouped)
                {
                    // Day header
                    var dayBorder = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 3, 8, 3),
                        Margin = new Thickness(0, 6, 0, 4)
                    };
                    var daySp = new StackPanel { Orientation = Orientation.Horizontal };
                    daySp.Children.Add(new TextBlock { Text = dayGroup.Key.ToString("dd.MM.yyyy (dddd)"), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0")) });
                    daySp.Children.Add(new TextBlock { Text = $"  — {dayGroup.Count()} pkt", FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E")), VerticalAlignment = VerticalAlignment.Center });
                    dayBorder.Child = daySp;
                    detList.Children.Add(dayBorder);

                    foreach (var (typ, tresc, hodowca, data) in dayGroup)
                    {
                        string icon = typ switch { "Telefon" => "\U0001F4DE", "Notatka" => "\U0001F4DD", "Zmiana statusu" => "\U0001F504", _ => "\U0001F4CB" };
                        string borderColor = typ switch { "Telefon" => "#22C55E", "Notatka" => "#3B82F6", "Zmiana statusu" => "#F59E0B", _ => "#475569" };

                        var itemBorder = new Border
                        {
                            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B")),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(10, 5, 10, 5),
                            Margin = new Thickness(0, 0, 0, 2),
                            BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(borderColor)),
                            BorderThickness = new Thickness(3, 0, 0, 0)
                        };

                        var itemStack = new StackPanel();

                        // Row 1: icon + type + hodowca
                        var row1 = new StackPanel { Orientation = Orientation.Horizontal };
                        row1.Children.Add(new TextBlock { Text = icon, FontSize = 12, Margin = new Thickness(0, 0, 5, 0), VerticalAlignment = VerticalAlignment.Center });
                        row1.Children.Add(new TextBlock { Text = typ, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(borderColor)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                        row1.Children.Add(new TextBlock { Text = hodowca, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#A5B4FC")), VerticalAlignment = VerticalAlignment.Center });
                        itemStack.Children.Add(row1);

                        // Row 2: tresc
                        if (!string.IsNullOrEmpty(tresc))
                        {
                            itemStack.Children.Add(new TextBlock
                            {
                                Text = tresc,
                                FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")),
                                TextWrapping = TextWrapping.Wrap, MaxHeight = 34, TextTrimming = TextTrimming.CharacterEllipsis,
                                Margin = new Thickness(0, 1, 0, 0)
                            });
                        }

                        // Row 3: time
                        itemStack.Children.Add(new TextBlock
                        {
                            Text = data.ToString("HH:mm"),
                            FontSize = 9, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")),
                            Margin = new Thickness(0, 1, 0, 0)
                        });

                        itemBorder.Child = itemStack;
                        detList.Children.Add(itemBorder);
                    }
                }
            }
            catch (Exception ex)
            {
                detList.Children.Clear();
                detList.Children.Add(new TextBlock { Text = $"Błąd: {ex.Message}", Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FCA5A5")), FontSize = 11 });
            }
        }

        private static void AddTextToGrid(Grid grid, string text, int col, double fontSize, string color, FontWeight weight, HorizontalAlignment hAlign = HorizontalAlignment.Left)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = weight,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = hAlign
            };
            Grid.SetColumn(tb, col);
            grid.Children.Add(tb);
        }

        #endregion

        #region Context Menu

        private void MenuGoogle_Click(object sender, RoutedEventArgs e)
        {
            if (dgHodowcy.SelectedItem is DataRowView drv)
            {
                string nazwa = drv["Dostawca"]?.ToString();
                string miejscowosc = drv["Miejscowosc"]?.ToString();
                string query = Uri.EscapeDataString($"{nazwa} {miejscowosc} hodowca drobiu");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"https://www.google.com/search?q={query}",
                    UseShellExecute = true
                });
            }
        }

        private void MenuMapa_Click(object sender, RoutedEventArgs e)
        {
            if (dgHodowcy.SelectedItem is DataRowView drv)
            {
                string ulica = drv["Ulica"]?.ToString();
                string kod = drv["KodPocztowy"]?.ToString();
                string miasto = drv["Miejscowosc"]?.ToString();
                string adres = $"{ulica}, {kod} {miasto}".Trim().Trim(',').Trim();
                if (!string.IsNullOrWhiteSpace(adres))
                {
                    string query = Uri.EscapeDataString(adres);
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = $"https://www.google.com/maps/search/?api=1&query={query}",
                        UseShellExecute = true
                    });
                }
            }
        }

        private void BtnMapa_MouseClick(object sender, MouseButtonEventArgs e)
        {
            BtnMapa_Click(sender, null);
        }

        private void BtnDuplikaty_Click(object sender, RoutedEventArgs e)
        {
            var dupWindow = new HodowcyDuplicateWindow(connectionString);
            dupWindow.Owner = this;
            dupWindow.ShowDialog();
            // Refresh after possible merges
            BtnRefresh_Click(null, null);
        }

        private void BtnMapa_Click(object sender, RoutedEventArgs e)
        {
            if (dtHodowcy == null) return;
            int? focusId = aktualnyHodowcaId > 0 ? aktualnyHodowcaId : null;
            var mapaWindow = new HodowcyMapaWindow(connectionString, dtHodowcy, focusId);
            mapaWindow.Show();
        }

        #endregion

        #region Delivery Search

        private void TxtSzukajDostawy_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                SzukajDostawy();
        }

        private void BtnSzukajDostawy_Click(object sender, RoutedEventArgs e)
        {
            SzukajDostawy();
        }

        private async void SzukajDostawy()
        {
            string input = txtSzukajDostawy.Text?.Trim();
            if (string.IsNullOrEmpty(input)) return;

            var words = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                             .Where(w => w.Length >= 2).ToArray();
            if (words.Length == 0) return;

            stackWynikiDostawy.Children.Clear();
            panelWynikiDostawy.Visibility = Visibility.Visible;

            // Loading indicator with spinner
            var loadingSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            var spinner = new ProgressBar
            {
                IsIndeterminate = true,
                Width = 80,
                Height = 4,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E")),
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            loadingSp.Children.Add(spinner);
            loadingSp.Children.Add(new TextBlock
            {
                Text = "Szukam w bazie dostaw...",
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")),
                FontSize = 11,
                FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center
            });
            stackWynikiDostawy.Children.Add(loadingSp);

            try
            {
                var results = new List<(string Name, int Count, DateTime First, DateTime Last)>();

                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();

                    // Build WHERE clause with LIKE for each word
                    var conditions = new List<string>();
                    for (int i = 0; i < words.Length; i++)
                        conditions.Add($"CustomerName LIKE @w{i}");

                    string sql = $@"SELECT CustomerName, COUNT(*) AS Dostawy,
                                           MIN(CONVERT(date, CreateData)) AS Pierwsza,
                                           MAX(CONVERT(date, CreateData)) AS Ostatnia
                                    FROM PartiaDostawca
                                    WHERE ({string.Join(" AND ", conditions)})
                                    GROUP BY CustomerName
                                    ORDER BY MAX(CreateData) DESC";

                    using var cmd = new SqlCommand(sql, conn);
                    for (int i = 0; i < words.Length; i++)
                        cmd.Parameters.AddWithValue($"@w{i}", $"%{words[i]}%");

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        results.Add((
                            reader["CustomerName"]?.ToString()?.Trim() ?? "",
                            Convert.ToInt32(reader["Dostawy"]),
                            Convert.ToDateTime(reader["Pierwsza"]),
                            Convert.ToDateTime(reader["Ostatnia"])
                        ));
                    }
                });

                stackWynikiDostawy.Children.Clear();

                if (results.Count == 0)
                {
                    var noResult = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#7F1D1D")),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(10, 8, 10, 8)
                    };
                    var noTxt = new TextBlock
                    {
                        Text = "Brak wyników — hodowca nigdy nie dostarczał",
                        Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FCA5A5")),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold
                    };
                    noResult.Child = noTxt;
                    stackWynikiDostawy.Children.Add(noResult);
                }
                else
                {
                    foreach (var (name, count, first, last) in results)
                    {
                        var card = new Border
                        {
                            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14532D")),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(10, 6, 10, 6),
                            Margin = new Thickness(0, 0, 0, 4),
                            BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E")),
                            BorderThickness = new Thickness(0, 0, 0, 1),
                            Cursor = Cursors.Hand,
                            ToolTip = "Kliknij aby zobaczyć szczegóły i dostawy"
                        };

                        string capturedName = name;
                        card.MouseLeftButtonDown += (s, args) =>
                        {
                            ShowDostawcaDetails(capturedName);
                        };

                        var sp = new StackPanel();

                        var nameTb = new TextBlock
                        {
                            Text = name,
                            FontSize = 12,
                            FontWeight = FontWeights.Bold,
                            Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4ADE80"))
                        };
                        sp.Children.Add(nameTb);

                        var statsSp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };

                        var countBadge = new Border
                        {
                            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F172A")),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(6, 2, 6, 2),
                            Margin = new Thickness(0, 0, 8, 0)
                        };
                        countBadge.Child = new TextBlock
                        {
                            Text = $"{count} partii",
                            FontSize = 10,
                            FontWeight = FontWeights.Bold,
                            Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E"))
                        };
                        statsSp.Children.Add(countBadge);

                        statsSp.Children.Add(new TextBlock
                        {
                            Text = $"{first:dd.MM.yyyy} — {last:dd.MM.yyyy}",
                            FontSize = 10,
                            Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")),
                            VerticalAlignment = VerticalAlignment.Center
                        });

                        sp.Children.Add(statsSp);
                        card.Child = sp;
                        stackWynikiDostawy.Children.Add(card);
                    }
                }
            }
            catch (Exception ex)
            {
                stackWynikiDostawy.Children.Clear();
                stackWynikiDostawy.Children.Add(new TextBlock
                {
                    Text = $"Błąd: {ex.Message}",
                    Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FCA5A5")),
                    FontSize = 10
                });
            }
        }

        private async void ShowDostawcaDetails(string customerName)
        {
            var dialog = new Window
            {
                Title = $"Dostawca — {customerName}",
                Width = 700,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F172A")),
                ResizeMode = ResizeMode.CanResize
            };
            WindowIconHelper.SetIcon(dialog);

            var mainStack = new StackPanel { Margin = new Thickness(16) };

            // Header
            var header = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14532D")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var headerSp = new StackPanel();
            headerSp.Children.Add(new TextBlock
            {
                Text = customerName,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4ADE80"))
            });
            var loadingTxt = new TextBlock
            {
                Text = "Ładowanie danych...",
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B"))
            };
            headerSp.Children.Add(loadingTxt);
            header.Child = headerSp;
            mainStack.Children.Add(header);

            var scrollHost = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var contentStack = new StackPanel();
            scrollHost.Content = contentStack;

            var scrollBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E293B")),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14),
                BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                BorderThickness = new Thickness(1)
            };
            scrollBorder.Child = scrollHost;

            var dockPanel = new DockPanel { LastChildFill = true };
            DockPanel.SetDock(mainStack, Dock.Top);
            dockPanel.Children.Add(mainStack);
            dockPanel.Children.Add(new Border { Margin = new Thickness(16, 0, 16, 16), Child = scrollBorder });
            dialog.Content = dockPanel;
            dialog.Show();

            try
            {
                string dostawcaName = null, address = null, postalCode = null, city = null;
                string phone1 = null, phone2 = null, phone3 = null;
                decimal? distance = null;
                int deliveryCount = 0;
                DateTime? pierwsza = null, ostatnia = null;
                var dostawy = new List<(DateTime Data, int Ile, string Godziny)>();

                await Task.Run(() =>
                {
                    using var conn = new SqlConnection(connectionString);
                    conn.Open();

                    // Dane adresowe z Dostawcy
                    using var cmd = new SqlCommand(@"SELECT TOP 1 d.Name, d.Address, d.PostalCode, d.City,
                                                           d.Phone1, d.Phone2, d.Phone3, d.Distance
                                                    FROM PartiaDostawca p
                                                    JOIN Dostawcy d ON p.CustomerID = d.ID
                                                    WHERE p.CustomerName = @name", conn);
                    cmd.Parameters.AddWithValue("@name", customerName);
                    using var reader = cmd.ExecuteReader();
                    if (reader.Read())
                    {
                        dostawcaName = reader["Name"] is DBNull ? "" : reader["Name"]?.ToString()?.Trim();
                        address = reader["Address"] is DBNull ? "" : reader["Address"]?.ToString()?.Trim();
                        postalCode = reader["PostalCode"] is DBNull ? "" : reader["PostalCode"]?.ToString()?.Trim();
                        city = reader["City"] is DBNull ? "" : reader["City"]?.ToString()?.Trim();
                        phone1 = reader["Phone1"] is DBNull ? "" : reader["Phone1"]?.ToString()?.Trim();
                        phone2 = reader["Phone2"] is DBNull ? "" : reader["Phone2"]?.ToString()?.Trim();
                        phone3 = reader["Phone3"] is DBNull ? "" : reader["Phone3"]?.ToString()?.Trim();
                        if (reader["Distance"] is not DBNull) distance = Convert.ToDecimal(reader["Distance"]);
                    }
                    reader.Close();

                    // Statystyki + dostawy pogrupowane po dniach
                    using var cmd2 = new SqlCommand(@"SELECT COUNT(*) AS Ilosc,
                                                            MIN(CONVERT(date, CreateData)) AS Pierwsza,
                                                            MAX(CONVERT(date, CreateData)) AS Ostatnia
                                                     FROM PartiaDostawca WHERE CustomerName = @name", conn);
                    cmd2.Parameters.AddWithValue("@name", customerName);
                    using var r2 = cmd2.ExecuteReader();
                    if (r2.Read())
                    {
                        deliveryCount = Convert.ToInt32(r2["Ilosc"]);
                        if (r2["Pierwsza"] is not DBNull) pierwsza = Convert.ToDateTime(r2["Pierwsza"]);
                        if (r2["Ostatnia"] is not DBNull) ostatnia = Convert.ToDateTime(r2["Ostatnia"]);
                    }
                    r2.Close();

                    using var cmd3 = new SqlCommand(@"SELECT CONVERT(date, CreateData) AS Dzien, COUNT(*) AS Ile,
                                                            STRING_AGG(ISNULL(CreateGodzina,''), ', ') AS Godziny
                                                     FROM PartiaDostawca WHERE CustomerName = @name
                                                     GROUP BY CONVERT(date, CreateData)
                                                     ORDER BY CONVERT(date, CreateData) DESC", conn);
                    cmd3.Parameters.AddWithValue("@name", customerName);
                    using var r3 = cmd3.ExecuteReader();
                    while (r3.Read())
                    {
                        dostawy.Add((
                            Convert.ToDateTime(r3["Dzien"]),
                            Convert.ToInt32(r3["Ile"]),
                            r3["Godziny"] is DBNull ? "" : r3["Godziny"]?.ToString()?.Trim() ?? ""
                        ));
                    }
                });

                loadingTxt.Text = dostawcaName != null
                    ? $"Dane z bazy dostawców  |  {deliveryCount} partii łącznie"
                    : $"{deliveryCount} partii łącznie";

                // === DANE HODOWCY ===
                if (dostawcaName != null)
                {
                    var infoLabel = new TextBlock
                    {
                        Text = "\U0001F4CB  DANE DOSTAWCY",
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#60A5FA")),
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    contentStack.Children.Add(infoLabel);

                    void AddRow(string label, string value)
                    {
                        if (string.IsNullOrWhiteSpace(value)) return;
                        var g = new Grid { Margin = new Thickness(0, 2, 0, 0) };
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        var lbl = new TextBlock { Text = label, FontSize = 12, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")), FontWeight = FontWeights.SemiBold };
                        Grid.SetColumn(lbl, 0);
                        g.Children.Add(lbl);
                        var val = new TextBlock { Text = value, FontSize = 12, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E2E8F0")), TextWrapping = TextWrapping.Wrap };
                        Grid.SetColumn(val, 1);
                        g.Children.Add(val);
                        contentStack.Children.Add(g);
                    }

                    AddRow("Nazwa", dostawcaName);
                    string fullAddr = "";
                    if (!string.IsNullOrEmpty(address)) fullAddr += address;
                    if (!string.IsNullOrEmpty(postalCode) || !string.IsNullOrEmpty(city))
                        fullAddr += (fullAddr.Length > 0 ? ", " : "") + $"{postalCode} {city}".Trim();
                    AddRow("Adres", fullAddr);
                    if (distance.HasValue) AddRow("Odległość", $"{distance.Value} km");
                    AddRow("Telefon 1", FormatPhone(phone1));
                    AddRow("Telefon 2", FormatPhone(phone2));
                    AddRow("Telefon 3", FormatPhone(phone3));

                    // Separator
                    contentStack.Children.Add(new Border
                    {
                        Height = 1,
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                        Margin = new Thickness(0, 10, 0, 8)
                    });
                }

                // === HISTORIA DOSTAW ===
                if (deliveryCount > 0)
                {
                    var delivLabel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                    delivLabel.Children.Add(new TextBlock { Text = "\U0001F69A  HISTORIA DOSTAW", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4ADE80")) });
                    if (pierwsza.HasValue && ostatnia.HasValue)
                    {
                        delivLabel.Children.Add(new TextBlock
                        {
                            Text = $"   {pierwsza.Value:dd.MM.yyyy} — {ostatnia.Value:dd.MM.yyyy}",
                            FontSize = 10,
                            Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8")),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                    }
                    contentStack.Children.Add(delivLabel);

                    // Nagłówki kolumn
                    var colHead = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                    colHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                    colHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                    colHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                    colHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    AddTextToGrid(colHead, "Data", 0, 9, "#64748B", FontWeights.Bold);
                    AddTextToGrid(colHead, "Dzień tygodnia", 1, 9, "#64748B", FontWeights.Bold);
                    AddTextToGrid(colHead, "Partii", 2, 9, "#64748B", FontWeights.Bold);
                    AddTextToGrid(colHead, "Godziny", 3, 9, "#64748B", FontWeights.Bold);
                    contentStack.Children.Add(colHead);

                    contentStack.Children.Add(new Border { Height = 1, Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")), Margin = new Thickness(0, 0, 0, 2) });

                    int nr = 0;
                    foreach (var (data, ile, godziny) in dostawy)
                    {
                        nr++;
                        var row = new Border
                        {
                            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(nr % 2 == 0 ? "#273548" : "#1E293B")),
                            Padding = new Thickness(4, 3, 4, 3)
                        };
                        var rg = new Grid();
                        rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                        rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                        rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
                        rg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        AddTextToGrid(rg, data.ToString("dd.MM.yyyy"), 0, 11, "#E2E8F0", FontWeights.SemiBold);
                        AddTextToGrid(rg, data.ToString("dddd"), 1, 10, "#94A3B8", FontWeights.Normal);

                        var ileBadge = new Border
                        {
                            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14532D")),
                            CornerRadius = new CornerRadius(3),
                            Padding = new Thickness(5, 1, 5, 1),
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        ileBadge.Child = new TextBlock { Text = ile.ToString(), FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22C55E")) };
                        Grid.SetColumn(ileBadge, 2);
                        rg.Children.Add(ileBadge);

                        if (!string.IsNullOrWhiteSpace(godziny))
                        {
                            var godzTxt = new TextBlock { Text = godziny, FontSize = 9, Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#64748B")), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                            Grid.SetColumn(godzTxt, 3);
                            rg.Children.Add(godzTxt);
                        }

                        row.Child = rg;
                        contentStack.Children.Add(row);
                    }
                }
            }
            catch (Exception ex)
            {
                loadingTxt.Text = $"Błąd: {ex.Message}";
            }
        }

        #endregion

        #region Helpers

        private void UpdateOstatniaAktywnosc(DataRowView drv, string tresc)
        {
            drv["OstatniUserId"] = App.UserID;
            drv["OstatniUserNazwa"] = App.UserFullName;
            drv["OstatniaTresc"] = tresc;
            drv["OstatniaData"] = DateTime.Now;

            // Odśwież avatar w wierszu DataGrid
            try
            {
                var row = dgHodowcy.ItemContainerGenerator.ContainerFromItem(drv) as DataGridRow;
                if (row != null)
                {
                    var img = FindVisualChild<System.Windows.Controls.Image>(row, "imgAvatar");
                    if (img != null)
                        img.Source = GetUserAvatar(App.UserID, App.UserFullName, 22);
                }
            }
            catch { }
        }

        private void InsertAktywnosc(SqlConnection conn, int hodowcaId, string typ, string tresc, string wynikTelefonu, string statusPrzed, string statusPo)
        {
            string sql = @"INSERT INTO Pozyskiwanie_Aktywnosci (HodowcaId, TypAktywnosci, Tresc, WynikTelefonu, StatusPrzed, StatusPo, UzytkownikId, UzytkownikNazwa)
                           VALUES (@hodowcaId, @typ, @tresc, @wynik, @statusPrzed, @statusPo, @userId, @userName)";
            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@hodowcaId", hodowcaId);
            cmd.Parameters.AddWithValue("@typ", typ);
            cmd.Parameters.AddWithValue("@tresc", tresc);
            cmd.Parameters.AddWithValue("@wynik", (object)wynikTelefonu ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@statusPrzed", (object)statusPrzed ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@statusPo", (object)statusPo ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@userId", App.UserID);
            cmd.Parameters.AddWithValue("@userName", App.UserFullName);
            cmd.ExecuteNonQuery();
        }

        private void ShowToast(string message)
        {
            toastText.Text = message;
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromSeconds(2) };
            toastPopup.BeginAnimation(OpacityProperty, null);
            toastPopup.Opacity = 0;
            var sb = new Storyboard();
            sb.Children.Add(fadeIn);
            sb.Children.Add(fadeOut);
            Storyboard.SetTarget(fadeIn, toastPopup);
            Storyboard.SetTarget(fadeOut, toastPopup);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
            Storyboard.SetTargetProperty(fadeOut, new PropertyPath(OpacityProperty));
            sb.Begin();
        }

        #endregion

        #region Models

        public class AktywnoscModel
        {
            public int Id { get; set; }
            public string TypAktywnosci { get; set; }
            public string Tresc { get; set; }
            public string WynikTelefonu { get; set; }
            public string StatusPrzed { get; set; }
            public string StatusPo { get; set; }
            public string UzytkownikId { get; set; }
            public string UzytkownikNazwa { get; set; }
            public DateTime DataUtworzenia { get; set; }

            public string IkonaTypu => TypAktywnosci switch
            {
                "Notatka" => "\U0001F4DD",
                "Telefon" => "\U0001F4DE",
                "Zmiana statusu" => "\U0001F504",
                "Przypisanie" => "\U0001F464",
                _ => "\U0001F4CB"
            };
        }

        #endregion
    }
}
