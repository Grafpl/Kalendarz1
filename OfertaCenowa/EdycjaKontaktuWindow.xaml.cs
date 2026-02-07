using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.OfertaCenowa
{
    public partial class EdycjaKontaktuWindow : Window
    {
        private readonly string _connectionString =
            "Server=192.168.0.109;Database=LibraNet;User Id=pronova;Password=pronova;TrustServerCertificate=True;";

        // Mapowanie prefiksu kodu pocztowego na województwo
        private static readonly Dictionary<string, string> kodDoWojewodztwa = new Dictionary<string, string>
        {
            {"00", "mazowieckie"}, {"01", "mazowieckie"}, {"02", "mazowieckie"}, {"03", "mazowieckie"}, {"04", "mazowieckie"}, {"05", "mazowieckie"},
            {"06", "mazowieckie"}, {"07", "mazowieckie"}, {"08", "mazowieckie"}, {"09", "mazowieckie"},
            {"10", "warmińsko-mazurskie"}, {"11", "warmińsko-mazurskie"}, {"12", "warmińsko-mazurskie"}, {"13", "warmińsko-mazurskie"}, {"14", "warmińsko-mazurskie"},
            {"15", "podlaskie"}, {"16", "podlaskie"}, {"17", "podlaskie"}, {"18", "podlaskie"}, {"19", "podlaskie"},
            {"20", "lubelskie"}, {"21", "lubelskie"}, {"22", "lubelskie"}, {"23", "lubelskie"}, {"24", "lubelskie"},
            {"25", "świętokrzyskie"}, {"26", "świętokrzyskie"}, {"27", "świętokrzyskie"}, {"28", "świętokrzyskie"}, {"29", "świętokrzyskie"},
            {"30", "małopolskie"}, {"31", "małopolskie"}, {"32", "małopolskie"}, {"33", "małopolskie"}, {"34", "małopolskie"},
            {"35", "podkarpackie"}, {"36", "podkarpackie"}, {"37", "podkarpackie"}, {"38", "podkarpackie"}, {"39", "podkarpackie"},
            {"40", "śląskie"}, {"41", "śląskie"}, {"42", "śląskie"}, {"43", "śląskie"}, {"44", "śląskie"},
            {"45", "opolskie"}, {"46", "opolskie"}, {"47", "opolskie"}, {"48", "opolskie"}, {"49", "opolskie"},
            {"50", "dolnośląskie"}, {"51", "dolnośląskie"}, {"52", "dolnośląskie"}, {"53", "dolnośląskie"}, {"54", "dolnośląskie"},
            {"55", "dolnośląskie"}, {"56", "dolnośląskie"}, {"57", "dolnośląskie"}, {"58", "dolnośląskie"}, {"59", "dolnośląskie"},
            {"60", "wielkopolskie"}, {"61", "wielkopolskie"}, {"62", "wielkopolskie"}, {"63", "wielkopolskie"}, {"64", "wielkopolskie"},
            {"65", "lubuskie"}, {"66", "lubuskie"}, {"67", "lubuskie"}, {"68", "lubuskie"}, {"69", "lubuskie"},
            {"70", "zachodniopomorskie"}, {"71", "zachodniopomorskie"}, {"72", "zachodniopomorskie"}, {"73", "zachodniopomorskie"}, {"74", "zachodniopomorskie"},
            {"75", "zachodniopomorskie"}, {"76", "zachodniopomorskie"}, {"77", "pomorskie"}, {"78", "zachodniopomorskie"},
            {"80", "pomorskie"}, {"81", "pomorskie"}, {"82", "pomorskie"}, {"83", "pomorskie"}, {"84", "pomorskie"},
            {"85", "kujawsko-pomorskie"}, {"86", "kujawsko-pomorskie"}, {"87", "kujawsko-pomorskie"}, {"88", "kujawsko-pomorskie"}, {"89", "kujawsko-pomorskie"},
            {"90", "łódzkie"}, {"91", "łódzkie"}, {"92", "łódzkie"}, {"93", "łódzkie"}, {"94", "łódzkie"},
            {"95", "łódzkie"}, {"96", "łódzkie"}, {"97", "łódzkie"}, {"98", "łódzkie"}, {"99", "łódzkie"}
        };

        public int KlientID { get; set; }
        public string KlientNazwa { get; set; }
        public string OperatorID { get; set; }

        public bool ZapisanoZmiany { get; private set; } = false;
        public string NowyEmail { get; private set; }
        public string NowyTelefon { get; private set; }
        public string NoweImie { get; private set; }
        public string NoweNazwisko { get; private set; }

        // Model dla notatki
        private class NotatkaCRM
        {
            public int ID { get; set; }
            public string Data { get; set; }
            public string Autor { get; set; }
            public string Tresc { get; set; }
            public double MaxHeight { get; set; } = 36; // Domyślnie zwinięta (2 linie)
            public bool Rozwiniety { get; set; } = false;
        }

        private List<NotatkaCRM> _notatki = new List<NotatkaCRM>();

        public EdycjaKontaktuWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            Loaded += EdycjaKontaktuWindow_Loaded;
        }

        private void EdycjaKontaktuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SprawdzIUtworzKolumny(); // Upewnij się, że wszystkie kolumny istnieją przed wczytaniem
            WczytajWojewodztwa();
            WczytajPKD();
            WczytajDaneKontaktowe();
            WczytajNotatki();

            // Obsługa placeholdera
            txtNotatki.TextChanged += (s, ev) =>
            {
                txtNotatkaPlaceholder.Visibility = string.IsNullOrEmpty(txtNotatki.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
            };
        }

        private void WczytajWojewodztwa()
        {
            cmbWojewodztwo.Items.Clear();
            cmbWojewodztwo.Items.Add("");
            cmbWojewodztwo.Items.Add("dolnośląskie");
            cmbWojewodztwo.Items.Add("kujawsko-pomorskie");
            cmbWojewodztwo.Items.Add("lubelskie");
            cmbWojewodztwo.Items.Add("lubuskie");
            cmbWojewodztwo.Items.Add("łódzkie");
            cmbWojewodztwo.Items.Add("małopolskie");
            cmbWojewodztwo.Items.Add("mazowieckie");
            cmbWojewodztwo.Items.Add("opolskie");
            cmbWojewodztwo.Items.Add("podkarpackie");
            cmbWojewodztwo.Items.Add("podlaskie");
            cmbWojewodztwo.Items.Add("pomorskie");
            cmbWojewodztwo.Items.Add("śląskie");
            cmbWojewodztwo.Items.Add("świętokrzyskie");
            cmbWojewodztwo.Items.Add("warmińsko-mazurskie");
            cmbWojewodztwo.Items.Add("wielkopolskie");
            cmbWojewodztwo.Items.Add("zachodniopomorskie");
        }

        private void WczytajPKD()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new SqlCommand("SELECT DISTINCT PKD_Opis FROM OdbiorcyCRM WHERE PKD_Opis IS NOT NULL AND PKD_Opis <> '' ORDER BY PKD_Opis", conn);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbPKD.Items.Add(reader.GetString(0));
                        }
                    }
                }
            }
            catch { }
        }

        private void WczytajDaneKontaktowe()
        {
            if (KlientID <= 0) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    const string sql = @"
                        SELECT Nazwa, PKD_Opis, KOD, MIASTO, Ulica, Wojewodztwo,
                               Imie, Nazwisko, Stanowisko, Email, TELEFON_K, TelefonDodatkowy,
                               Status, Tagi, NIP, REGON, KRS, WWW, FormaPrawna, Powiat, Gmina,
                               Zrodlo, Fax, EmailDodatkowy
                        FROM OdbiorcyCRM
                        WHERE ID = @ID";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@ID", KlientID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                txtNazwa.Text = GetStringOrEmpty(reader, 0);
                                cmbPKD.Text = GetStringOrEmpty(reader, 1);
                                txtKod.Text = GetStringOrEmpty(reader, 2);
                                txtMiasto.Text = GetStringOrEmpty(reader, 3);
                                txtUlica.Text = GetStringOrEmpty(reader, 4);

                                string woj = GetStringOrEmpty(reader, 5);
                                int idx = cmbWojewodztwo.Items.IndexOf(woj.ToLower());
                                if (idx >= 0) cmbWojewodztwo.SelectedIndex = idx;

                                txtImie.Text = GetStringOrEmpty(reader, 6);
                                txtNazwisko.Text = GetStringOrEmpty(reader, 7);
                                txtStanowisko.Text = GetStringOrEmpty(reader, 8);
                                txtEmail.Text = GetStringOrEmpty(reader, 9);
                                txtTelefon.Text = GetStringOrEmpty(reader, 10);
                                txtTelefonDodatkowy.Text = GetStringOrEmpty(reader, 11);

                                // Status
                                string status = reader.IsDBNull(12) ? "Do zadzwonienia" : reader.GetString(12);
                                for (int i = 0; i < cmbStatus.Items.Count; i++)
                                {
                                    if (cmbStatus.Items[i] is ComboBoxItem item && item.Content.ToString() == status)
                                    {
                                        cmbStatus.SelectedIndex = i;
                                        break;
                                    }
                                }

                                txtTagi.Text = GetStringOrEmpty(reader, 13);
                                txtNIP.Text = GetStringOrEmpty(reader, 14);
                                txtREGON.Text = GetStringOrEmpty(reader, 15);
                                txtKRS.Text = GetStringOrEmpty(reader, 16);
                                txtWWW.Text = GetStringOrEmpty(reader, 17);

                                string formaPrawna = GetStringOrEmpty(reader, 18);
                                for (int i = 0; i < cmbFormaPrawna.Items.Count; i++)
                                {
                                    if (cmbFormaPrawna.Items[i] is ComboBoxItem item && item.Content.ToString() == formaPrawna)
                                    {
                                        cmbFormaPrawna.SelectedIndex = i;
                                        break;
                                    }
                                }

                                txtPowiat.Text = GetStringOrEmpty(reader, 19);
                                txtGmina.Text = GetStringOrEmpty(reader, 20);

                                string zrodlo = GetStringOrEmpty(reader, 21);
                                for (int i = 0; i < cmbZrodlo.Items.Count; i++)
                                {
                                    if (cmbZrodlo.Items[i] is ComboBoxItem item && item.Content.ToString() == zrodlo)
                                    {
                                        cmbZrodlo.SelectedIndex = i;
                                        break;
                                    }
                                }

                                txtFax.Text = GetStringOrEmpty(reader, 22);
                                txtEmailDodatkowy.Text = GetStringOrEmpty(reader, 23);

                                txtKlientNazwa.Text = " — " + txtNazwa.Text;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd wczytywania: {ex.Message}";
            }
        }

        private string GetStringOrEmpty(SqlDataReader reader, int index)
        {
            try
            {
                return reader.IsDBNull(index) ? "" : reader.GetString(index);
            }
            catch
            {
                return "";
            }
        }

        private void WczytajNotatki()
        {
            if (KlientID <= 0) return;

            try
            {
                _notatki.Clear();

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        SELECT ID, Tresc, KtoDodal, DataDodania
                        FROM NotatkiCRM
                        WHERE IDOdbiorcy = @id
                        ORDER BY DataDodania DESC", conn);
                    cmd.Parameters.AddWithValue("@id", KlientID);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var notatka = new NotatkaCRM
                            {
                                ID = reader.GetInt32(0),
                                Tresc = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Autor = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                Data = reader.IsDBNull(3) ? "" : reader.GetDateTime(3).ToString("dd.MM.yyyy HH:mm"),
                                MaxHeight = 36 // Domyślnie zwinięta
                            };
                            _notatki.Add(notatka);
                        }
                    }
                }

                listaNotatek.ItemsSource = null;
                listaNotatek.ItemsSource = _notatki;
            }
            catch { }
        }

        private void TxtNotatki_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && !string.IsNullOrWhiteSpace(txtNotatki.Text))
            {
                DodajNotatke();
                e.Handled = true;
            }
        }

        private void DodajNotatke()
        {
            if (KlientID <= 0 || string.IsNullOrWhiteSpace(txtNotatki.Text)) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    var cmd = new SqlCommand(@"
                        INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal, DataDodania)
                        VALUES (@id, @tresc, @kto, GETDATE())", conn);
                    cmd.Parameters.AddWithValue("@id", KlientID);
                    cmd.Parameters.AddWithValue("@tresc", txtNotatki.Text.Trim());
                    cmd.Parameters.AddWithValue("@kto", OperatorID ?? "");
                    cmd.ExecuteNonQuery();
                }

                txtNotatki.Text = "";
                WczytajNotatki();
                txtStatus.Text = "Notatka dodana";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd dodawania notatki: {ex.Message}";
            }
        }

        private void Notatka_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is NotatkaCRM notatka)
            {
                // Toggle rozwinięcia
                notatka.Rozwiniety = !notatka.Rozwiniety;
                notatka.MaxHeight = notatka.Rozwiniety ? double.PositiveInfinity : 36;

                // Odśwież binding
                listaNotatek.ItemsSource = null;
                listaNotatek.ItemsSource = _notatki;
            }
        }

        private async void BtnPobierzNIP_Click(object sender, RoutedEventArgs e)
        {
            string nip = txtNIP.Text?.Trim().Replace("-", "").Replace(" ", "");
            if (string.IsNullOrEmpty(nip) || nip.Length != 10)
            {
                MessageBox.Show("Podaj prawidłowy numer NIP (10 cyfr)", "NIP", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNIP.Focus();
                return;
            }

            txtStatus.Text = "Pobieranie danych z REGON...";
            btnPobierzNIP.IsEnabled = false;

            try
            {
                var dane = await PobierzDaneZRegon(nip);
                if (dane != null)
                {
                    if (!string.IsNullOrEmpty(dane.Nazwa)) txtNazwa.Text = dane.Nazwa;
                    if (!string.IsNullOrEmpty(dane.Regon)) txtREGON.Text = dane.Regon;
                    if (!string.IsNullOrEmpty(dane.KodPocztowy)) txtKod.Text = dane.KodPocztowy;
                    if (!string.IsNullOrEmpty(dane.Miasto)) txtMiasto.Text = dane.Miasto;
                    if (!string.IsNullOrEmpty(dane.Ulica)) txtUlica.Text = dane.Ulica;
                    if (!string.IsNullOrEmpty(dane.Wojewodztwo))
                    {
                        string woj = dane.Wojewodztwo.ToLower();
                        int idx = cmbWojewodztwo.Items.IndexOf(woj);
                        if (idx >= 0) cmbWojewodztwo.SelectedIndex = idx;
                    }
                    if (!string.IsNullOrEmpty(dane.Powiat)) txtPowiat.Text = dane.Powiat;
                    if (!string.IsNullOrEmpty(dane.Gmina)) txtGmina.Text = dane.Gmina;
                    if (!string.IsNullOrEmpty(dane.PKD)) cmbPKD.Text = dane.PKD;

                    txtStatus.Text = "Dane pobrane pomyślnie!";
                }
                else
                {
                    txtStatus.Text = "Nie znaleziono danych dla podanego NIP";
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Błąd: {ex.Message}";
            }
            finally
            {
                btnPobierzNIP.IsEnabled = true;
            }
        }

        private async Task<DaneFirmy> PobierzDaneZRegon(string nip)
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                // Oficjalne API Ministerstwa Finansów - Biała Lista VAT
                string today = DateTime.Now.ToString("yyyy-MM-dd");
                var response = await client.GetAsync($"https://wl-api.mf.gov.pl/api/search/nip/{nip}?date={today}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("result", out var result) &&
                        result.TryGetProperty("subject", out var subject))
                    {
                        var dane = new DaneFirmy
                        {
                            Nazwa = GetJsonString(subject, "name"),
                            Regon = GetJsonString(subject, "regon")
                        };

                        // Parsuj adres z workingAddress lub residenceAddress
                        string adres = GetJsonString(subject, "workingAddress");
                        if (string.IsNullOrEmpty(adres))
                            adres = GetJsonString(subject, "residenceAddress");

                        if (!string.IsNullOrEmpty(adres))
                        {
                            ParseAdres(adres, dane);
                        }

                        return dane;
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private void ParseAdres(string adres, DaneFirmy dane)
        {
            // Format: "ul. Nazwa 123, 00-000 Miasto" lub "Miejscowość, ul. Nazwa 123, 00-000 Miasto"
            try
            {
                // Szukaj kodu pocztowego (XX-XXX)
                var kodMatch = System.Text.RegularExpressions.Regex.Match(adres, @"(\d{2}-\d{3})");
                if (kodMatch.Success)
                {
                    dane.KodPocztowy = kodMatch.Groups[1].Value;

                    // Miasto jest po kodzie pocztowym
                    int kodIndex = adres.IndexOf(dane.KodPocztowy);
                    if (kodIndex >= 0)
                    {
                        string poKodzie = adres.Substring(kodIndex + dane.KodPocztowy.Length).Trim();
                        // Usuń przecinek na początku jeśli jest
                        if (poKodzie.StartsWith(",")) poKodzie = poKodzie.Substring(1).Trim();
                        dane.Miasto = poKodzie;
                    }

                    // Ulica jest przed kodem pocztowym
                    string przedKodem = adres.Substring(0, kodIndex).Trim();
                    if (przedKodem.EndsWith(",")) przedKodem = przedKodem.Substring(0, przedKodem.Length - 1).Trim();
                    dane.Ulica = przedKodem;
                }
                else
                {
                    // Brak kodu - cały adres to ulica
                    dane.Ulica = adres;
                }
            }
            catch
            {
                dane.Ulica = adres;
            }
        }

        private string GetJsonString(JsonElement element, string propertyName)
        {
            try
            {
                if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
                    return prop.GetString() ?? "";
                return "";
            }
            catch { return ""; }
        }

        private void TxtKod_TextChanged(object sender, TextChangedEventArgs e)
        {
            string kod = txtKod.Text.Replace("-", "").Trim();

            if (kod.Length >= 2)
            {
                string prefix = kod.Substring(0, 2);

                // Auto-wypełnianie województwa na podstawie prefiksu kodu
                if (kodDoWojewodztwa.TryGetValue(prefix, out string woj))
                {
                    int index = cmbWojewodztwo.Items.IndexOf(woj);
                    if (index >= 0)
                        cmbWojewodztwo.SelectedIndex = index;
                }

                // Gdy mamy pełny kod (5 cyfr), szukaj miasta, powiatu i gminy w bazie
                if (kod.Length >= 5)
                {
                    try
                    {
                        using (var conn = new SqlConnection(_connectionString))
                        {
                            conn.Open();

                            // Szukaj w tabeli KodyPocztowe
                            var cmd = new SqlCommand(@"
                                SELECT TOP 1 miej FROM KodyPocztowe
                                WHERE REPLACE(Kod, '-', '') = @kod", conn);
                            cmd.Parameters.AddWithValue("@kod", kod);
                            var miasto = cmd.ExecuteScalar() as string;

                            if (!string.IsNullOrEmpty(miasto) && string.IsNullOrEmpty(txtMiasto.Text))
                            {
                                txtMiasto.Text = miasto;
                            }

                            // Szukaj w OdbiorcyCRM dla powiatu i gminy
                            var cmd2 = new SqlCommand(@"
                                SELECT TOP 1 MIASTO, Wojewodztwo, Powiat, Gmina
                                FROM OdbiorcyCRM
                                WHERE REPLACE(KOD, '-', '') = @kod
                                  AND (Powiat IS NOT NULL OR Gmina IS NOT NULL)", conn);
                            cmd2.Parameters.AddWithValue("@kod", kod);
                            using (var reader = cmd2.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    if (string.IsNullOrEmpty(txtMiasto.Text) && !reader.IsDBNull(0))
                                        txtMiasto.Text = reader.GetString(0);

                                    if (!reader.IsDBNull(1))
                                    {
                                        string wojDB = reader.GetString(1)?.ToLower();
                                        if (!string.IsNullOrEmpty(wojDB))
                                        {
                                            int idx = cmbWojewodztwo.Items.IndexOf(wojDB);
                                            if (idx >= 0)
                                                cmbWojewodztwo.SelectedIndex = idx;
                                        }
                                    }

                                    if (string.IsNullOrEmpty(txtPowiat.Text) && !reader.IsDBNull(2))
                                        txtPowiat.Text = reader.GetString(2);

                                    if (string.IsNullOrEmpty(txtGmina.Text) && !reader.IsDBNull(3))
                                        txtGmina.Text = reader.GetString(3);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        private void BtnZapisz_Click(object sender, RoutedEventArgs e)
        {
            if (KlientID <= 0)
            {
                MessageBox.Show("Brak ID klienta.", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtNazwa.Text))
            {
                MessageBox.Show("Podaj nazwę firmy!", "Brak nazwy", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtNazwa.Focus();
                return;
            }

            try
            {
                SprawdzIUtworzKolumny();

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Get selected values from comboboxes
                    string selectedStatus = "Do zadzwonienia";
                    if (cmbStatus.SelectedItem is ComboBoxItem statusItem)
                        selectedStatus = statusItem.Content.ToString();

                    string selectedFormaPrawna = cmbFormaPrawna.Text;
                    string selectedZrodlo = cmbZrodlo.Text;

                    const string sql = @"
                        UPDATE OdbiorcyCRM
                        SET Nazwa = @Nazwa,
                            PKD_Opis = @PKD,
                            KOD = @Kod,
                            MIASTO = @Miasto,
                            Ulica = @Ulica,
                            Wojewodztwo = @Woj,
                            Imie = @Imie,
                            Nazwisko = @Nazwisko,
                            Stanowisko = @Stanowisko,
                            Email = @Email,
                            TELEFON_K = @Telefon,
                            TelefonDodatkowy = @TelefonDodatkowy,
                            Status = @Status,
                            Tagi = @Tagi,
                            NIP = @NIP,
                            REGON = @REGON,
                            KRS = @KRS,
                            WWW = @WWW,
                            FormaPrawna = @FormaPrawna,
                            Powiat = @Powiat,
                            Gmina = @Gmina,
                            Zrodlo = @Zrodlo,
                            Fax = @Fax,
                            EmailDodatkowy = @EmailDodatkowy
                        WHERE ID = @ID";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@Nazwa", txtNazwa.Text ?? "");
                        cmd.Parameters.AddWithValue("@PKD", cmbPKD.Text ?? "");
                        cmd.Parameters.AddWithValue("@Kod", txtKod.Text ?? "");
                        cmd.Parameters.AddWithValue("@Miasto", txtMiasto.Text ?? "");
                        cmd.Parameters.AddWithValue("@Ulica", txtUlica.Text ?? "");
                        cmd.Parameters.AddWithValue("@Woj", cmbWojewodztwo.Text ?? "");
                        cmd.Parameters.AddWithValue("@Imie", txtImie.Text ?? "");
                        cmd.Parameters.AddWithValue("@Nazwisko", txtNazwisko.Text ?? "");
                        cmd.Parameters.AddWithValue("@Stanowisko", txtStanowisko.Text ?? "");
                        cmd.Parameters.AddWithValue("@Email", txtEmail.Text ?? "");
                        cmd.Parameters.AddWithValue("@Telefon", txtTelefon.Text ?? "");
                        cmd.Parameters.AddWithValue("@TelefonDodatkowy", txtTelefonDodatkowy.Text ?? "");
                        cmd.Parameters.AddWithValue("@Status", selectedStatus);
                        cmd.Parameters.AddWithValue("@Tagi", txtTagi.Text ?? "");
                        cmd.Parameters.AddWithValue("@NIP", txtNIP.Text ?? "");
                        cmd.Parameters.AddWithValue("@REGON", txtREGON.Text ?? "");
                        cmd.Parameters.AddWithValue("@KRS", txtKRS.Text ?? "");
                        cmd.Parameters.AddWithValue("@WWW", txtWWW.Text ?? "");
                        cmd.Parameters.AddWithValue("@FormaPrawna", selectedFormaPrawna ?? "");
                        cmd.Parameters.AddWithValue("@Powiat", txtPowiat.Text ?? "");
                        cmd.Parameters.AddWithValue("@Gmina", txtGmina.Text ?? "");
                        cmd.Parameters.AddWithValue("@Zrodlo", selectedZrodlo ?? "");
                        cmd.Parameters.AddWithValue("@Fax", txtFax.Text ?? "");
                        cmd.Parameters.AddWithValue("@EmailDodatkowy", txtEmailDodatkowy.Text ?? "");
                        cmd.Parameters.AddWithValue("@ID", KlientID);

                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected > 0)
                        {
                            ZapiszHistorie(conn);
                            ZapisanoZmiany = true;
                            NowyEmail = txtEmail.Text;
                            NowyTelefon = txtTelefon.Text;
                            NoweImie = txtImie.Text;
                            NoweNazwisko = txtNazwisko.Text;

                            MessageBox.Show($"Dane kontrahenta zostały zapisane.", "Zapisano",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                            DialogResult = true;
                            Close();
                        }
                        else
                        {
                            MessageBox.Show("Nie znaleziono klienta w bazie.", "Błąd",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd zapisu:\n{ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SprawdzIUtworzKolumny()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string[] kolumny = new[] {
                        "Email", "TelefonDodatkowy", "Imie", "Nazwisko", "Stanowisko",
                        "NIP", "REGON", "KRS", "WWW", "FormaPrawna", "Powiat", "Gmina",
                        "Zrodlo", "Fax", "EmailDodatkowy"
                    };

                    foreach (var kolumna in kolumny)
                    {
                        string checkSql = $@"
                            IF NOT EXISTS (
                                SELECT * FROM sys.columns
                                WHERE object_id = OBJECT_ID('OdbiorcyCRM') AND name = '{kolumna}'
                            )
                            BEGIN
                                ALTER TABLE OdbiorcyCRM ADD {kolumna} NVARCHAR(200) NULL
                            END";

                        using (var cmd = new SqlCommand(checkSql, conn))
                        {
                            cmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd tworzenia kolumn: {ex.Message}");
            }
        }

        private void ZapiszHistorie(SqlConnection conn)
        {
            try
            {
                const string sql = @"
                    INSERT INTO HistoriaZmianCRM (IDOdbiorcy, TypZmiany, WartoscNowa, KtoWykonal, DataZmiany)
                    VALUES (@ID, @Typ, @Wartosc, @Operator, GETDATE())";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@ID", KlientID);
                    cmd.Parameters.AddWithValue("@Typ", "Edycja danych kontaktowych");
                    cmd.Parameters.AddWithValue("@Wartosc", $"Email: {txtEmail.Text}, Tel: {txtTelefon.Text}, NIP: {txtNIP.Text}");
                    cmd.Parameters.AddWithValue("@Operator", OperatorID ?? "");
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    internal class DaneFirmy
    {
        public string Nazwa { get; set; }
        public string Regon { get; set; }
        public string KodPocztowy { get; set; }
        public string Miasto { get; set; }
        public string Ulica { get; set; }
        public string Wojewodztwo { get; set; }
        public string Powiat { get; set; }
        public string Gmina { get; set; }
        public string PKD { get; set; }
    }
}
