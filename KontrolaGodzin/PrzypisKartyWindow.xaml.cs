using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Kalendarz1.KontrolaGodzin
{
    public partial class PrzypisKartyWindow : Window
    {
        private readonly string _connectionString = @"Server=192.168.0.23\SQLEXPRESS;Database=UNISYSTEM;User Id=sa;Password=UniRCPAdmin123$;";

        private List<PracownikDoKarty> _pracownicyBezKarty = new List<PracownikDoKarty>();
        private List<PracownikDoKarty> _pracownicyZKarta = new List<PracownikDoKarty>();
        private List<WolnaKarta> _wolneKarty = new List<WolnaKarta>();

        public bool ZmianyZapisane { get; private set; } = false;

        public PrzypisKartyWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
            Loaded += (s, e) => LoadData();
        }

        private void LoadData()
        {
            try
            {
                LoadPracownicyBezKarty();
                LoadPracownicyZKarta();
                LoadWolneKarty();
                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPracownicyBezKarty()
        {
            _pracownicyBezKarty.Clear();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Pracownicy aktywni (typ 1) bez aktualnie przypisanej karty
                string sql = @"
                    SELECT 
                        e.RCINE_EMPLOYEE_ID,
                        e.RCINE_EMPLOYEE_NAME,
                        e.RCINE_EMPLOYEE_SURNAME,
                        e.RCINE_EMPLOYEE_GROUP_NAME
                    FROM V_RCINE_EMPLOYEES e
                    WHERE e.RCINE_EMPLOYEE_TYPE = 1
                      AND NOT EXISTS (
                          SELECT 1 FROM V_KDINEC_EMPLOYEES_CARDS ec 
                          WHERE ec.KDINEC_EMPLOYEE_ID = e.RCINE_EMPLOYEE_ID 
                            AND ec.KDINEC_DATETIME_TO IS NULL
                      )
                      AND e.RCINE_EMPLOYEE_GROUP_NAME NOT LIKE '%Zwolnieni%'
                      AND e.RCINE_EMPLOYEE_GROUP_NAME NOT LIKE '%WYJECHALI%'
                    ORDER BY e.RCINE_EMPLOYEE_SURNAME, e.RCINE_EMPLOYEE_NAME";

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _pracownicyBezKarty.Add(new PracownikDoKarty
                        {
                            Id = reader.GetInt32(0),
                            Imie = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Nazwisko = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            GrupaNazwa = reader.IsDBNull(3) ? "" : reader.GetString(3)
                        });
                    }
                }
            }
        }

        private void LoadPracownicyZKarta()
        {
            _pracownicyZKarta.Clear();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Pracownicy z aktualnie przypisaną kartą
                string sql = @"
                    SELECT 
                        e.RCINE_EMPLOYEE_ID,
                        e.RCINE_EMPLOYEE_NAME,
                        e.RCINE_EMPLOYEE_SURNAME,
                        e.RCINE_EMPLOYEE_GROUP_NAME,
                        ec.KDINEC_CARD_NUMBER,
                        ec.KDINEC_DATETIME_FROM,
                        c.KDCAC_ID
                    FROM V_RCINE_EMPLOYEES e
                    INNER JOIN V_KDINEC_EMPLOYEES_CARDS ec ON e.RCINE_EMPLOYEE_ID = ec.KDINEC_EMPLOYEE_ID
                    INNER JOIN T_KDCAC_CARDS c ON ec.KDINEC_CARD_NUMBER = c.KDCAC_NUMBER
                    WHERE e.RCINE_EMPLOYEE_TYPE = 1
                      AND ec.KDINEC_DATETIME_TO IS NULL
                    ORDER BY e.RCINE_EMPLOYEE_SURNAME, e.RCINE_EMPLOYEE_NAME";

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        _pracownicyZKarta.Add(new PracownikDoKarty
                        {
                            Id = reader.GetInt32(0),
                            Imie = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            Nazwisko = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            GrupaNazwa = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            NumerKarty = reader.IsDBNull(4) ? 0 : reader.GetInt64(4),
                            KartaPrzypisanaOd = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                            KartaId = reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                        });
                    }
                }
            }
        }

        private void LoadWolneKarty()
        {
            _wolneKarty.Clear();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                // Karty aktywne (stan 135270402) bez aktualnego przypisania
                string sql = @"
                    SELECT 
                        c.KDCAC_ID,
                        c.KDCAC_NUMBER,
                        c.KDCAC_STATE
                    FROM T_KDCAC_CARDS c
                    WHERE c.KDCAC_STATE = 135270402  -- Aktywna
                      AND NOT EXISTS (
                          SELECT 1 FROM T_KDCAUC_USERS_CARDS uc 
                          WHERE uc.KDCAUC_ID_KDCAC_CARD = c.KDCAC_ID 
                            AND uc.KDCAUC_TO IS NULL
                      )
                    ORDER BY c.KDCAC_NUMBER";

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var stan = reader.GetInt32(2);
                        _wolneKarty.Add(new WolnaKarta
                        {
                            KartaId = reader.GetInt32(0),
                            NumerKarty = reader.GetInt64(1),
                            Stan = stan,
                            StanOpis = stan == 135270402 ? "✅ Aktywna" : "❓ " + stan
                        });
                    }
                }
            }
        }

        private void UpdateUI()
        {
            bool trybPrzypisz = rbPrzypisz.IsChecked == true;

            // Aktualizuj etykietę
            txtPracownikLabel.Text = trybPrzypisz ? "Pracownik bez karty:" : "Pracownik z kartą:";

            // Aktualizuj listę pracowników
            var lista = trybPrzypisz ? _pracownicyBezKarty : _pracownicyZKarta;
            var szukaj = txtSzukajPracownika.Text?.Trim().ToLower() ?? "";

            if (!string.IsNullOrEmpty(szukaj))
            {
                lista = lista.Where(p =>
                    p.PelneNazwisko.ToLower().Contains(szukaj) ||
                    p.GrupaNazwa.ToLower().Contains(szukaj)).ToList();
            }

            cmbPracownik.ItemsSource = lista;
            cmbPracownik.SelectedIndex = -1;

            // Pokaż/ukryj panele
            panelKarta.Visibility = trybPrzypisz ? Visibility.Visible : Visibility.Collapsed;
            panelProfile.Visibility = trybPrzypisz ? Visibility.Visible : Visibility.Collapsed;
            panelAktualnaKarta.Visibility = Visibility.Collapsed;
            panelInfoPracownik.Visibility = Visibility.Collapsed;
            panelAktualneProfile.Visibility = Visibility.Collapsed;

            // Aktualizuj listę kart
            cmbKarta.ItemsSource = _wolneKarty;
            cmbKarta.SelectedIndex = -1;
            txtWolneKarty.Text = $"Dostępnych kart: {_wolneKarty.Count}";

            // Przycisk
            btnWykonaj.Content = trybPrzypisz ? "➕ Przypisz kartę" : "➖ Odbierz kartę";
            btnWykonaj.Style = trybPrzypisz 
                ? (Style)FindResource("PrimaryButton") 
                : (Style)FindResource("DangerButton");
            btnWykonaj.IsEnabled = false;

            txtStatus.Text = trybPrzypisz 
                ? $"Pracowników bez karty: {_pracownicyBezKarty.Count}" 
                : $"Pracowników z kartą: {_pracownicyZKarta.Count}";
        }

        private void Tryb_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) UpdateUI();
        }

        private void TxtSzukajPracownika_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded) UpdateUI();
        }

        private void CmbPracownik_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var pracownik = cmbPracownik.SelectedItem as PracownikDoKarty;

            if (pracownik != null)
            {
                panelInfoPracownik.Visibility = Visibility.Visible;
                txtPracownikId.Text = pracownik.Id.ToString();
                txtPracownikDzial.Text = pracownik.GrupaNazwa;

                // Tryb odbierz - pokaż aktualną kartę
                if (rbOdbierz.IsChecked == true)
                {
                    panelAktualnaKarta.Visibility = Visibility.Visible;
                    txtAktualnaKartaNumer.Text = pracownik.NumerKarty.ToString();
                    txtAktualnaKartaOd.Text = pracownik.KartaPrzypisanaOd?.ToString("dd.MM.yyyy HH:mm") ?? "-";
                    txtAktualnaKartaId.Text = pracownik.KartaId.ToString();
                }
                else
                {
                    // Tryb przypisz - sprawdź aktualne profile pracownika
                    SprawdzAktualneProfile(pracownik.Id);
                }

                ValidateForm();
            }
            else
            {
                panelInfoPracownik.Visibility = Visibility.Collapsed;
                panelAktualnaKarta.Visibility = Visibility.Collapsed;
                panelAktualneProfile.Visibility = Visibility.Collapsed;
                btnWykonaj.IsEnabled = false;
            }
        }

        private void SprawdzAktualneProfile(int pracownikId)
        {
            try
            {
                var profile = new List<string>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT KDPRPU_ID_KDPRP_PROFILE
                        FROM T_KDPRPU_PROFILES_USERS
                        WHERE KDPRPU_ID_UXUSUD_USER = @PracownikId";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@PracownikId", pracownikId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int profilId = reader.GetInt32(0);
                                if (profilId == 10001) profile.Add("Portiernia");
                                if (profilId == 10002) profile.Add("Produkcja");
                            }
                        }
                    }
                }

                if (profile.Count > 0)
                {
                    panelAktualneProfile.Visibility = Visibility.Visible;
                    txtAktualneProfile.Text = $"Pracownik ma już przypisane profile: {string.Join(", ", profile)}. " +
                                              "Zaznaczone profile zostaną dodane (duplikaty pominięte).";

                    // Zaznacz checkboxy dla istniejących profili
                    if (chkPortiernia != null) chkPortiernia.IsChecked = profile.Contains("Portiernia");
                    if (chkProdukcja != null) chkProdukcja.IsChecked = profile.Contains("Produkcja");
                }
                else
                {
                    panelAktualneProfile.Visibility = Visibility.Collapsed;
                    if (chkPortiernia != null) chkPortiernia.IsChecked = true;
                    if (chkProdukcja != null) chkProdukcja.IsChecked = true;
                }
            }
            catch
            {
                panelAktualneProfile.Visibility = Visibility.Collapsed;
            }
        }

        private void CmbKarta_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateForm();
        }

        private void ChkProfil_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) ValidateForm();
        }

        private void ValidateForm()
        {
            bool trybPrzypisz = rbPrzypisz.IsChecked == true;
            var pracownik = cmbPracownik.SelectedItem as PracownikDoKarty;
            var karta = cmbKarta.SelectedItem as WolnaKarta;

            if (trybPrzypisz)
            {
                bool maProfilZaznaczony = (chkPortiernia?.IsChecked == true) || (chkProdukcja?.IsChecked == true);
                btnWykonaj.IsEnabled = pracownik != null && karta != null && maProfilZaznaczony;
                
                if (pracownik != null && karta != null && !maProfilZaznaczony)
                {
                    txtStatus.Text = "⚠️ Zaznacz przynajmniej jeden profil dostępu!";
                }
                else if (pracownik != null && karta != null)
                {
                    var profile = new List<string>();
                    if (chkPortiernia?.IsChecked == true) profile.Add("Portiernia");
                    if (chkProdukcja?.IsChecked == true) profile.Add("Produkcja");
                    txtStatus.Text = $"Gotowe: {pracownik.PelneNazwisko} ← Karta #{karta.NumerKarty} + {string.Join(", ", profile)}";
                }
                else
                {
                    txtStatus.Text = "Wybierz pracownika i kartę";
                }
            }
            else
            {
                btnWykonaj.IsEnabled = pracownik != null && pracownik.KartaId > 0;
                txtStatus.Text = pracownik != null
                    ? $"Gotowe do odebrania: Karta #{pracownik.NumerKarty} od {pracownik.PelneNazwisko}"
                    : "Wybierz pracownika z kartą";
            }
        }

        private void BtnWykonaj_Click(object sender, RoutedEventArgs e)
        {
            bool trybPrzypisz = rbPrzypisz.IsChecked == true;

            if (trybPrzypisz)
                PrzypiszKarte();
            else
                OdbierzKarte();
        }

        private void PrzypiszKarte()
        {
            var pracownik = cmbPracownik.SelectedItem as PracownikDoKarty;
            var karta = cmbKarta.SelectedItem as WolnaKarta;

            if (pracownik == null || karta == null) return;

            // Sprawdź jakie profile do dodania
            var profileDoDodania = new List<string>();
            if (chkPortiernia?.IsChecked == true) profileDoDodania.Add("Portiernia (ID: 10001)");
            if (chkProdukcja?.IsChecked == true) profileDoDodania.Add("Produkcja (ID: 10002)");

            var wynik = MessageBox.Show(
                $"Czy na pewno przypisać kartę #{karta.NumerKarty} pracownikowi:\n\n" +
                $"👤 {pracownik.PelneNazwisko}\n" +
                $"📁 {pracownik.GrupaNazwa}\n\n" +
                $"🚪 Profile dostępu:\n" +
                $"   {(profileDoDodania.Count > 0 ? string.Join("\n   ", profileDoDodania) : "Brak (tylko karta)")}\n\n" +
                $"Karta zostanie aktywowana od teraz.",
                "Potwierdzenie przypisania",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (wynik != MessageBoxResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // 1. Przypisz kartę
                    string sqlKarta = @"
                        INSERT INTO T_KDCAUC_USERS_CARDS 
                            (KDCAUC_ID_KDCAC_CARD, KDCAUC_ID_UXUSUD_USER, KDCAUC_FROM, KDCAUC_TO)
                        VALUES 
                            (@KartaId, @PracownikId, GETDATE(), NULL)";

                    using (var cmd = new SqlCommand(sqlKarta, conn))
                    {
                        cmd.Parameters.AddWithValue("@KartaId", karta.KartaId);
                        cmd.Parameters.AddWithValue("@PracownikId", pracownik.Id);
                        cmd.ExecuteNonQuery();
                    }

                    // 2. Przypisz profile (jeśli nie istnieją)
                    int dodaneProfile = 0;

                    if (chkPortiernia?.IsChecked == true)
                    {
                        dodaneProfile += DodajProfilJesliNieIstnieje(conn, pracownik.Id, 10001);
                    }

                    if (chkProdukcja?.IsChecked == true)
                    {
                        dodaneProfile += DodajProfilJesliNieIstnieje(conn, pracownik.Id, 10002);
                    }

                    ZmianyZapisane = true;

                    var komunikat = $"✅ Karta #{karta.NumerKarty} została przypisana!\n\n" +
                                    $"Pracownik: {pracownik.PelneNazwisko}\n" +
                                    $"Od: {DateTime.Now:dd.MM.yyyy HH:mm}\n";

                    if (dodaneProfile > 0)
                    {
                        komunikat += $"\n🚪 Dodano {dodaneProfile} nowych profili dostępu.";
                    }

                    komunikat += "\n\nKarta i profile zostaną zsynchronizowane z czytnikami.";

                    MessageBox.Show(komunikat, "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Odśwież dane
                    LoadData();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas przypisywania karty:\n\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int DodajProfilJesliNieIstnieje(SqlConnection conn, int pracownikId, int profilId)
        {
            // Sprawdź czy profil już istnieje
            string sqlCheck = @"
                SELECT COUNT(*) FROM T_KDPRPU_PROFILES_USERS 
                WHERE KDPRPU_ID_UXUSUD_USER = @PracownikId 
                  AND KDPRPU_ID_KDPRP_PROFILE = @ProfilId";

            using (var cmdCheck = new SqlCommand(sqlCheck, conn))
            {
                cmdCheck.Parameters.AddWithValue("@PracownikId", pracownikId);
                cmdCheck.Parameters.AddWithValue("@ProfilId", profilId);

                int count = (int)cmdCheck.ExecuteScalar();
                if (count > 0) return 0; // Profil już istnieje
            }

            // Dodaj profil
            string sqlInsert = @"
                INSERT INTO T_KDPRPU_PROFILES_USERS 
                    (KDPRPU_ID_KDPRP_PROFILE, KDPRPU_ID_UXUSUD_USER, KDPRPU_PROFILE_VALIDITY_DATE_FOR_USER)
                VALUES 
                    (@ProfilId, @PracownikId, NULL)";

            using (var cmdInsert = new SqlCommand(sqlInsert, conn))
            {
                cmdInsert.Parameters.AddWithValue("@ProfilId", profilId);
                cmdInsert.Parameters.AddWithValue("@PracownikId", pracownikId);
                cmdInsert.ExecuteNonQuery();
            }

            return 1;
        }

        private void OdbierzKarte()
        {
            var pracownik = cmbPracownik.SelectedItem as PracownikDoKarty;

            if (pracownik == null || pracownik.KartaId <= 0) return;

            var wynik = MessageBox.Show(
                $"Czy na pewno odebrać kartę #{pracownik.NumerKarty} od pracownika:\n\n" +
                $"👤 {pracownik.PelneNazwisko}\n" +
                $"📁 {pracownik.GrupaNazwa}\n\n" +
                $"Karta została przypisana: {pracownik.KartaPrzypisanaOd:dd.MM.yyyy}",
                "Potwierdzenie odebrania",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (wynik != MessageBoxResult.Yes) return;

            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // UPDATE - ustaw datę końca przypisania
                    string sql = @"
                        UPDATE T_KDCAUC_USERS_CARDS 
                        SET KDCAUC_TO = GETDATE()
                        WHERE KDCAUC_ID_KDCAC_CARD = @KartaId 
                          AND KDCAUC_ID_UXUSUD_USER = @PracownikId
                          AND KDCAUC_TO IS NULL";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@KartaId", pracownik.KartaId);
                        cmd.Parameters.AddWithValue("@PracownikId", pracownik.Id);

                        int affected = cmd.ExecuteNonQuery();

                        if (affected > 0)
                        {
                            ZmianyZapisane = true;
                            MessageBox.Show(
                                $"✅ Karta #{pracownik.NumerKarty} została odebrana!\n\n" +
                                $"Pracownik: {pracownik.PelneNazwisko}\n" +
                                $"Karta jest teraz wolna i może być przypisana innemu pracownikowi.",
                                "Sukces",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            // Odśwież dane
                            LoadData();
                        }
                        else
                        {
                            MessageBox.Show("Nie znaleziono aktywnego przypisania do zaktualizowania.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas odbierania karty:\n\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAnuluj_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    #region Models

    public class PracownikDoKarty
    {
        public int Id { get; set; }
        public string Imie { get; set; }
        public string Nazwisko { get; set; }
        public string PelneNazwisko => $"{Nazwisko} {Imie}".Trim();
        public string GrupaNazwa { get; set; }
        
        // Dla pracowników z kartą
        public long NumerKarty { get; set; }
        public DateTime? KartaPrzypisanaOd { get; set; }
        public int KartaId { get; set; }
    }

    public class WolnaKarta
    {
        public int KartaId { get; set; }
        public long NumerKarty { get; set; }
        public int Stan { get; set; }
        public string StanOpis { get; set; }
    }

    #endregion
}
