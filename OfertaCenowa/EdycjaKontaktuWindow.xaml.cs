using System;
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

        public int KlientID { get; set; }
        public string KlientNazwa { get; set; }
        public string OperatorID { get; set; }

        public bool ZapisanoZmiany { get; private set; } = false;
        public string NowyEmail { get; private set; }
        public string NowyTelefon { get; private set; }
        public string NoweImie { get; private set; }
        public string NoweNazwisko { get; private set; }

        public EdycjaKontaktuWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            Loaded += EdycjaKontaktuWindow_Loaded;
        }

        private void EdycjaKontaktuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WczytajWojewodztwa();
            WczytajPKD();
            WczytajDaneKontaktowe();
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
                // Używamy API rejestr.io (darmowe do 100 zapytań dziennie)
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetAsync($"https://rejestr.io/api/v2/org?nip={nip}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
                    {
                        var item = items[0];
                        return new DaneFirmy
                        {
                            Nazwa = GetJsonString(item, "name"),
                            Regon = GetJsonString(item, "regon"),
                            KodPocztowy = GetJsonString(item, "postalCode"),
                            Miasto = GetJsonString(item, "city"),
                            Ulica = GetJsonString(item, "street"),
                            Wojewodztwo = GetJsonString(item, "voivodeship"),
                            Powiat = GetJsonString(item, "county"),
                            Gmina = GetJsonString(item, "community"),
                            PKD = GetJsonString(item, "mainPkd")
                        };
                    }
                }

                // Fallback - API KRS
                response = await client.GetAsync($"https://api-krs.pl/api/nip/{nip}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    return new DaneFirmy
                    {
                        Nazwa = GetJsonString(root, "nazwa"),
                        Regon = GetJsonString(root, "regon"),
                        KodPocztowy = GetJsonString(root, "kod_pocztowy"),
                        Miasto = GetJsonString(root, "miejscowosc"),
                        Ulica = GetJsonString(root, "ulica") + " " + GetJsonString(root, "nr_domu"),
                        Wojewodztwo = GetJsonString(root, "wojewodztwo"),
                        Powiat = GetJsonString(root, "powiat"),
                        Gmina = GetJsonString(root, "gmina")
                    };
                }

                return null;
            }
            catch
            {
                return null;
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

                            // Dodaj notatkę jeśli jest
                            if (!string.IsNullOrWhiteSpace(txtNotatki.Text))
                            {
                                var cmdNotatka = new SqlCommand(@"
                                    INSERT INTO NotatkiCRM (IDOdbiorcy, Tresc, KtoDodal)
                                    VALUES (@id, @tresc, @kto)", conn);
                                cmdNotatka.Parameters.AddWithValue("@id", KlientID);
                                cmdNotatka.Parameters.AddWithValue("@tresc", txtNotatki.Text.Trim());
                                cmdNotatka.Parameters.AddWithValue("@kto", OperatorID ?? "");
                                cmdNotatka.ExecuteNonQuery();
                            }

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
