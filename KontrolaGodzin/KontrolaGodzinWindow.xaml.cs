using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Documents;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Kalendarz1.KontrolaGodzin
{
    /// <summary>
    /// Kontrola Godzin Pracy - Moduł integracji z UNICARD
    /// Poprawione zapytania SQL na podstawie rzeczywistej struktury bazy
    /// </summary>
    public partial class KontrolaGodzinWindow : Window
    {
        // Connection string - dostosuj do swojego środowiska
        private readonly string _connectionString = @"Server=192.168.0.23\SQLEXPRESS;Database=UNISYSTEM;User Id=sa;Password=UniRCPAdmin123$;";
        
        private DispatcherTimer _timer;
        private List<RejestracjaModel> _wszystkieRejestracje = new List<RejestracjaModel>();
        private List<GrupaModel> _grupy = new List<GrupaModel>();
        private List<PracownikModel> _pracownicy = new List<PracownikModel>();

        public KontrolaGodzinWindow()
        {
            InitializeComponent();
            WindowIconHelper.SetIcon(this);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Inicjalizacja dat
                dpOd.SelectedDate = DateTime.Today;
                dpDo.SelectedDate = DateTime.Today;

                // Timer zegara
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += (s, ev) => UpdateClock();
                _timer.Start();
                UpdateClock();

                // Inicjalizacja combo-boxów
                InitializeCombos();

                // Ładowanie danych z overlayem
                loadingOverlay.Show("Ładowanie danych pracowników...");
                try
                {
                    LoadGrupy();
                    LoadPracownicy();
                    loadingOverlay.LoadingMessage = "Ładowanie rejestracji...";
                    LoadAllData();
                    loadingOverlay.LoadingMessage = "Ładowanie harmonogramów...";
                    LoadHarmonogramPrzerw();
                    LoadAgencjeTydzien();
                }
                finally
                {
                    loadingOverlay.Hide();
                }
            }
            catch (Exception ex)
            {
                loadingOverlay.Hide();
                System.Diagnostics.Debug.WriteLine($"Błąd inicjalizacji: {ex.Message}");
            }
        }

        private void UpdateClock()
        {
            txtAktualnaData.Text = DateTime.Now.ToString("dddd, dd MMMM yyyy");
            txtAktualnaGodzina.Text = DateTime.Now.ToString("HH:mm:ss");
            
            // Aktualizacja zakresu dat w nagłówku
            if (dpOd.SelectedDate.HasValue && dpDo.SelectedDate.HasValue)
            {
                if (dpOd.SelectedDate.Value == dpDo.SelectedDate.Value)
                    txtZakresDat.Text = $"Zakres: {dpOd.SelectedDate.Value:dd.MM.yyyy}";
                else
                    txtZakresDat.Text = $"Zakres: {dpOd.SelectedDate.Value:dd.MM} - {dpDo.SelectedDate.Value:dd.MM.yyyy}";
            }
        }

        private void InitializeCombos()
        {
            // Miesiące
            var miesiace = new[] { "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec",
                                   "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień" };
            cmbMiesiac.ItemsSource = miesiace;
            cmbMiesiacEwidencja.ItemsSource = miesiace;
            cmbAgencjaMiesiac.ItemsSource = miesiace;
            cmbMiesiac.SelectedIndex = DateTime.Now.Month - 1;
            cmbMiesiacEwidencja.SelectedIndex = DateTime.Now.Month - 1;
            cmbAgencjaMiesiac.SelectedIndex = DateTime.Now.Month - 1;

            // Lata
            var lata = Enumerable.Range(DateTime.Now.Year - 2, 5).ToList();
            cmbRok.ItemsSource = lata;
            cmbRokEwidencja.ItemsSource = lata;
            cmbAgencjaRok.ItemsSource = lata;
            cmbRok.SelectedItem = DateTime.Now.Year;
            cmbRokEwidencja.SelectedItem = DateTime.Now.Year;
            cmbAgencjaRok.SelectedItem = DateTime.Now.Year;

            // Porównanie miesięcy
            var miesiaceLata = new List<string>();
            for (int y = DateTime.Now.Year; y >= DateTime.Now.Year - 1; y--)
            {
                for (int m = 12; m >= 1; m--)
                {
                    if (y == DateTime.Now.Year && m > DateTime.Now.Month) continue;
                    miesiaceLata.Add($"{miesiace[m - 1]} {y}");
                }
            }
            cmbPorownanieMiesiac1.ItemsSource = miesiaceLata;
            cmbPorownanieMiesiac2.ItemsSource = miesiaceLata;
            if (miesiaceLata.Count >= 2)
            {
                cmbPorownanieMiesiac1.SelectedIndex = 0;
                cmbPorownanieMiesiac2.SelectedIndex = 1;
            }

            // Nowe zakładki - Urlopy
            cmbUrlopyMiesiac.ItemsSource = miesiace;
            cmbUrlopyMiesiac.SelectedIndex = DateTime.Now.Month - 1;
            cmbUrlopyRok.ItemsSource = lata;
            cmbUrlopyRok.SelectedItem = DateTime.Now.Year;

            // Spóźnienia
            cmbSpoznieniaMiesiac.ItemsSource = miesiace;
            cmbSpoznieniaMiesiac.SelectedIndex = DateTime.Now.Month - 1;
            cmbSpoznieniaRok.ItemsSource = lata;
            cmbSpoznieniaRok.SelectedItem = DateTime.Now.Year;

            // Przerwy
            dpPrzerwyData.SelectedDate = DateTime.Today;

            // Typy nieobecności
            cmbTypNieobecnosci.Items.Add("Wszystkie typy");
            cmbTypNieobecnosci.Items.Add("Urlop wypoczynkowy");
            cmbTypNieobecnosci.Items.Add("Urlop na żądanie");
            cmbTypNieobecnosci.Items.Add("Zwolnienie chorobowe");
            cmbTypNieobecnosci.Items.Add("Urlop okolicznościowy");
            cmbTypNieobecnosci.SelectedIndex = 0;

            // Agencje - tygodnie
            var tygodnie = new List<string>();
            var startTygodnia = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
            for (int i = 0; i < 10; i++)
            {
                var tydzien = startTygodnia.AddDays(-7 * i);
                tygodnie.Add($"{tydzien:dd.MM} - {tydzien.AddDays(6):dd.MM.yyyy}");
            }
            cmbAgencjaTydzien.ItemsSource = tygodnie;
            cmbAgencjaTydzien.SelectedIndex = 0;

            // Agencje - filtr
            cmbAgencjaFiltr.Items.Add("Wszystkie agencje");
            cmbAgencjaFiltr.Items.Add("GURAVO");
            cmbAgencjaFiltr.Items.Add("AGENCJA IMPULS");
            cmbAgencjaFiltr.SelectedIndex = 0;
        }

        #region Ładowanie danych z UNICARD

        /// <summary>
        /// Pobiera grupy pracowników z widoku V_RCINEG_EMPLOYEES_GROUPS
        /// </summary>
        private void LoadGrupy()
        {
            try
            {
                _grupy.Clear();
                _grupy.Add(new GrupaModel { Id = 0, Nazwa = "-- Wszystkie działy --" });

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    
                    // Prawidłowe kolumny z V_RCINEG_EMPLOYEES_GROUPS
                    string sql = @"
                        SELECT DISTINCT 
                            RCINEG_EMPLOYEE_GROUP_ID, 
                            RCINEG_EMPLOYEE_GROUP_NAME
                        FROM V_RCINEG_EMPLOYEES_GROUPS
                        WHERE RCINEG_EMPLOYEE_GROUP_NAME IS NOT NULL
                        ORDER BY RCINEG_EMPLOYEE_GROUP_NAME";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _grupy.Add(new GrupaModel
                            {
                                Id = reader.GetInt32(0),
                                Nazwa = reader.GetString(1)
                            });
                        }
                    }
                }

                cmbGrupa.ItemsSource = _grupy;
                cmbGrupa.DisplayMemberPath = "Nazwa";
                cmbGrupa.SelectedValuePath = "Id";
                cmbGrupa.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania grup: {ex.Message}");
                
                // Fallback - pusta lista
                if (cmbGrupa != null)
                {
                    cmbGrupa.ItemsSource = new[] { new GrupaModel { Id = 0, Nazwa = "-- Wszystkie działy --" } };
                    cmbGrupa.SelectedIndex = 0;
                }
            }
        }

        /// <summary>
        /// Pobiera listę pracowników z widoku V_RCINE_EMPLOYEES
        /// </summary>
        private void LoadPracownicy()
        {
            try
            {
                _pracownicy.Clear();
                _pracownicy.Add(new PracownikModel { Id = 0, Imie = "-- Wybierz", Nazwisko = "pracownika --" });

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    
                    // Prawidłowe kolumny z V_RCINE_EMPLOYEES
                    string sql = @"
                        SELECT 
                            RCINE_EMPLOYEE_ID,
                            RCINE_EMPLOYEE_NAME,
                            RCINE_EMPLOYEE_SURNAME,
                            RCINE_EMPLOYEE_GROUP_ID,
                            RCINE_EMPLOYEE_GROUP_NAME
                        FROM V_RCINE_EMPLOYEES
                        WHERE RCINE_EMPLOYEE_TYPE = 1  -- Tylko aktywni pracownicy (typ 1)
                        ORDER BY RCINE_EMPLOYEE_SURNAME, RCINE_EMPLOYEE_NAME";

                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            _pracownicy.Add(new PracownikModel
                            {
                                Id = reader.GetInt32(0),
                                Imie = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                Nazwisko = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                GrupaId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                                GrupaNazwa = reader.IsDBNull(4) ? "" : reader.GetString(4)
                            });
                        }
                    }
                }

                // Bezpieczne przypisanie ItemsSource - używamy nowych kopii listy
                var listaPracownikow = _pracownicy.ToList();
                
                if (cmbPracownikEwidencja != null)
                {
                    cmbPracownikEwidencja.ItemsSource = listaPracownikow;
                    cmbPracownikEwidencja.DisplayMemberPath = "PelneNazwisko";
                    cmbPracownikEwidencja.SelectedValuePath = "Id";
                    if (listaPracownikow.Count > 0) cmbPracownikEwidencja.SelectedIndex = 0;
                }

                if (cmbHistoriaPracownik != null)
                {
                    cmbHistoriaPracownik.ItemsSource = _pracownicy.ToList();
                    cmbHistoriaPracownik.DisplayMemberPath = "PelneNazwisko";
                    cmbHistoriaPracownik.SelectedValuePath = "Id";
                    if (_pracownicy.Count > 0) cmbHistoriaPracownik.SelectedIndex = 0;
                }

                // Alerty - filtr pracownika
                if (cmbAlertyPracownik != null)
                {
                    cmbAlertyPracownik.ItemsSource = _pracownicy.ToList();
                    cmbAlertyPracownik.DisplayMemberPath = "PelneNazwisko";
                    cmbAlertyPracownik.SelectedValuePath = "Id";
                    if (_pracownicy.Count > 0) cmbAlertyPracownik.SelectedIndex = 0;
                }

                // Nadgodziny - filtr pracownika
                if (cmbNadgodzinyPracownik != null)
                {
                    cmbNadgodzinyPracownik.ItemsSource = _pracownicy.ToList();
                    cmbNadgodzinyPracownik.DisplayMemberPath = "PelneNazwisko";
                    cmbNadgodzinyPracownik.SelectedValuePath = "Id";
                    if (_pracownicy.Count > 0) cmbNadgodzinyPracownik.SelectedIndex = 0;
                }

                // Załaduj listę agencji
                LoadAgencje();
            }
            catch (Exception ex)
            {
                // Cichy błąd - nie pokazujemy MessageBox podczas inicjalizacji
                System.Diagnostics.Debug.WriteLine($"Błąd ładowania pracowników: {ex.Message}");
            }
        }

        /// <summary>
        /// Główna metoda ładująca wszystkie rejestracje z V_KDINAR_ALL_REGISTRATIONS
        /// </summary>
        private void LoadAllData()
        {
            try
            {
                _wszystkieRejestracje.Clear();

                DateTime dataOd = dpOd.SelectedDate ?? DateTime.Today;
                DateTime dataDo = (dpDo.SelectedDate ?? DateTime.Today).AddDays(1);

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    // Prawidłowe kolumny z V_KDINAR_ALL_REGISTRATIONS
                    string sql = @"
                        SELECT 
                            KDINAR_REGISTRTN_DATETIME,
                            KDINAR_REGISTRTN_TYPE,
                            KDINAR_EMPLOYEE_ID,
                            KDINAR_EMPLOYEE_NAME,
                            KDINAR_EMPLOYEE_SURNAME,
                            KDINAR_EMPLOYEE_GROUP_ID,
                            KDINAR_ACCESS_POINT_NAME,
                            KDINAR_CARD_NUMBER,
                            KDINAR_DEVICE_NAME,
                            KDINAR_REGISTRTN_MODE
                        FROM V_KDINAR_ALL_REGISTRATIONS
                        WHERE KDINAR_REGISTRTN_DATETIME >= @DataOd 
                          AND KDINAR_REGISTRTN_DATETIME < @DataDo
                          AND KDINAR_EMPLOYEE_ID IS NOT NULL
                        ORDER BY KDINAR_REGISTRTN_DATETIME DESC";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", dataOd);
                        cmd.Parameters.AddWithValue("@DataDo", dataDo);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var punktDostepu = reader.IsDBNull(6) ? "" : reader.GetString(6);
                                var typZBazy = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                
                                // Określ typ wejścia/wyjścia na podstawie nazwy punktu dostępu
                                // "WY" w nazwie = wyjście, "WE" w nazwie = wejście
                                int typInt = OkreslTypWejsciaWyjscia(punktDostepu, typZBazy);
                                string typ = typInt == 1 ? "WEJŚCIE" : "WYJŚCIE";

                                var reg = new RejestracjaModel
                                {
                                    DataCzas = reader.GetDateTime(0),
                                    Typ = typ,
                                    TypInt = typInt,
                                    PracownikId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                    Pracownik = $"{(reader.IsDBNull(4) ? "" : reader.GetString(4))} {(reader.IsDBNull(3) ? "" : reader.GetString(3))}".Trim(),
                                    GrupaId = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                                    PunktDostepu = punktDostepu,
                                    NumerKarty = reader.IsDBNull(7) ? 0 : reader.GetInt64(7),
                                    Urzadzenie = reader.IsDBNull(8) ? "" : reader.GetString(8),
                                    TrybRejestracji = reader.IsDBNull(9) ? 0 : reader.GetInt32(9)
                                };

                                // Uzupełnij nazwę grupy
                                var grupa = _grupy.FirstOrDefault(g => g.Id == reg.GrupaId);
                                reg.Grupa = grupa?.Nazwa ?? "Brak działu";

                                // Ustaw typ punktu na podstawie nazwy
                                reg.TypPunktu = OkreslTypPunktu(reg.PunktDostepu);

                                _wszystkieRejestracje.Add(reg);
                            }
                        }
                    }
                }

                // Usuń duplikaty (pozwolenie + otwarcie bramki = 2 wpisy w ciągu kilku sekund)
                _wszystkieRejestracje = UsunDuplikaty(_wszystkieRejestracje);

                // Aktualizuj wszystkie widoki
                UpdateAllViews();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string OkreslTypPunktu(string nazwaPointu)
        {
            if (string.IsNullOrEmpty(nazwaPointu)) return "Nieznany";
            
            nazwaPointu = nazwaPointu.ToUpper();
            if (nazwaPointu.Contains("WEJŚCIE") || nazwaPointu.Contains("BRAMA") || nazwaPointu.Contains("GŁÓWN"))
                return "Brama główna";
            if (nazwaPointu.Contains("PRODUKCJ"))
                return "Produkcja";
            if (nazwaPointu.Contains("BIUR"))
                return "Biuro";
            if (nazwaPointu.Contains("MAGAZYN"))
                return "Magazyn";
            return "Punkt dostępu";
        }

        /// <summary>
        /// Określa typ wejścia/wyjścia na podstawie nazwy punktu dostępu
        /// WY w nazwie = wyjście (0), WE w nazwie = wejście (1)
        /// Jeśli brak WE/WY, używa wartości z bazy
        /// </summary>
        private int OkreslTypWejsciaWyjscia(string punktDostepu, int typZBazy)
        {
            if (string.IsNullOrEmpty(punktDostepu))
                return typZBazy;

            var nazwa = punktDostepu.ToUpper();
            
            // Sprawdź końcówkę nazwy - najczęściej "Portiernia WY" lub "Portiernia WE"
            // Lub "Produkcja WY", "Produkcja WE"
            if (nazwa.EndsWith(" WY") || nazwa.Contains(" WY ") || nazwa.Contains("_WY") || nazwa.EndsWith("_WY"))
                return 0; // Wyjście
            
            if (nazwa.EndsWith(" WE") || nazwa.Contains(" WE ") || nazwa.Contains("_WE") || nazwa.EndsWith("_WE"))
                return 1; // Wejście
            
            // Sprawdź też inne wzorce
            if (nazwa.Contains("WYJŚCIE") || nazwa.Contains("WYJSC") || nazwa.Contains("EXIT") || nazwa.Contains("OUT"))
                return 0; // Wyjście
                
            if (nazwa.Contains("WEJŚCIE") || nazwa.Contains("WEJSC") || nazwa.Contains("ENTRY") || nazwa.Contains("IN"))
                return 1; // Wejście

            // Fallback - użyj wartości z bazy danych
            return typZBazy;
        }

        /// <summary>
        /// Usuwa duplikaty rejestracji (pozwolenie + otwarcie bramki = 2 wpisy w ciągu kilku sekund)
        /// Zostawia tylko pierwszy wpis dla tego samego pracownika, typu i punktu w oknie 30 sekund
        /// </summary>
        private List<RejestracjaModel> UsunDuplikaty(List<RejestracjaModel> rejestracje)
        {
            var wynik = new List<RejestracjaModel>();
            var posortowane = rejestracje.OrderBy(r => r.DataCzas).ToList();

            foreach (var reg in posortowane)
            {
                // Sprawdź czy istnieje podobna rejestracja w ciągu ostatnich 30 sekund
                var isDuplicate = wynik.Any(r =>
                    r.PracownikId == reg.PracownikId &&
                    r.TypInt == reg.TypInt &&
                    r.PunktDostepu == reg.PunktDostepu &&
                    Math.Abs((reg.DataCzas - r.DataCzas).TotalSeconds) <= 30);

                if (!isDuplicate)
                {
                    wynik.Add(reg);
                }
            }

            // Przywróć kolejność malejącą (najnowsze najpierw)
            return wynik.OrderByDescending(r => r.DataCzas).ToList();
        }

        /// <summary>
        /// Ładuje listę agencji na podstawie nazw działów
        /// </summary>
        private void LoadAgencje()
        {
            var agencje = new List<string> { "-- Wszystkie agencje --" };
            
            // Wykryj agencje na podstawie nazw działów
            var wykryteAgencje = _grupy
                .Where(g => g.Nazwa != null && (
                    g.Nazwa.ToUpper().Contains("AGENCJA") ||
                    g.Nazwa.ToUpper().Contains("GURAVO") ||
                    g.Nazwa.ToUpper().Contains("IMPULS") ||
                    g.Nazwa.ToUpper().Contains("STAR") ||
                    g.Nazwa.ToUpper().Contains("ECO-MEN") ||
                    g.Nazwa.ToUpper().Contains("ROB-JOB")))
                .Select(g => g.Nazwa)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            agencje.AddRange(wykryteAgencje);
            
            // Dodaj też standardowe działy jako "własni pracownicy"
            var wlasne = _grupy
                .Where(g => g.Nazwa != null && !wykryteAgencje.Contains(g.Nazwa) && g.Id > 0)
                .Select(g => g.Nazwa)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            
            agencje.AddRange(wlasne);

            cmbAgencja.ItemsSource = agencje;
            cmbAgencja.SelectedIndex = 0;
            
            // Pokaż wykryte agencje
            icAgencje.ItemsSource = wykryteAgencje.Take(10);
        }

        #endregion

        #region Aktualizacja widoków

        private void UpdateAllViews()
        {
            // Filtruj dane
            var filteredData = FilterData();

            // Zakładka: Dashboard
            UpdateDashboard(filteredData);

            // Zakładka: Rejestracje
            UpdateRejestracje(filteredData);

            // Zakładka: Godziny Pracy
            UpdateGodzinyPracy(filteredData);

            // Zakładka: Obecni
            UpdateObecni(filteredData);

            // Zakładka: Podsumowanie
            UpdatePodsumowanie(filteredData);

            // Zakładka: Alerty
            UpdateAlerty(filteredData);

            // Zakładka: Ranking
            UpdateRanking(filteredData);

            // Zakładka: Nadgodziny
            UpdateNadgodziny(filteredData);

            // Zakładka: Punktualność
            UpdatePunktualnosc(filteredData);

            // Zakładka: Nieobecności
            UpdateNieobecnosci(filteredData);

            // Zakładka: Porównanie działów
            UpdatePorownanie(filteredData);

            // Aktualizuj nagłówek
            UpdateHeader(filteredData);
        }

        private List<RejestracjaModel> FilterData()
        {
            var filtered = _wszystkieRejestracje.AsEnumerable();

            // Filtr grupy
            if (cmbGrupa.SelectedValue is int grupaId && grupaId > 0)
            {
                filtered = filtered.Where(r => r.GrupaId == grupaId);
            }

            // Filtr szukania
            string szukaj = txtSzukaj?.Text?.Trim().ToLower() ?? "";
            if (!string.IsNullOrEmpty(szukaj))
            {
                filtered = filtered.Where(r =>
                    (r.Pracownik?.ToLower().Contains(szukaj) ?? false) ||
                    (r.Grupa?.ToLower().Contains(szukaj) ?? false) ||
                    (r.PunktDostepu?.ToLower().Contains(szukaj) ?? false));
            }

            return filtered.ToList();
        }

        private void UpdateHeader(List<RejestracjaModel> data)
        {
            // Oblicz liczbę obecnych (wejście bez wyjścia dzisiaj)
            var dzisiaj = data.Where(r => r.DataCzas.Date == DateTime.Today).ToList();
            var wejscia = dzisiaj.Where(r => r.TypInt == 1).GroupBy(r => r.PracownikId).ToDictionary(g => g.Key, g => g.Max(r => r.DataCzas));
            var wyjscia = dzisiaj.Where(r => r.TypInt == 0).GroupBy(r => r.PracownikId).ToDictionary(g => g.Key, g => g.Max(r => r.DataCzas));

            int obecni = 0;
            foreach (var w in wejscia)
            {
                if (!wyjscia.ContainsKey(w.Key) || wyjscia[w.Key] < w.Value)
                    obecni++;
            }

            txtLiczbaObecnych.Text = $"{obecni} obecnych";

            // Zakres dat
            DateTime dataOd = dpOd.SelectedDate ?? DateTime.Today;
            DateTime dataDo = dpDo.SelectedDate ?? DateTime.Today;
            if (dataOd == dataDo)
                txtZakresDat.Text = $"Zakres: {dataOd:dd.MM.yyyy}";
            else
                txtZakresDat.Text = $"Zakres: {dataOd:dd.MM} - {dataDo:dd.MM.yyyy}";
            
            // Aktualizuj stopkę
            txtLiczbaRejestracjiFooter.Text = $"{data.Count} rejestracji";
            txtOstatnieOdswiezenie.Text = $"Odświeżono: {DateTime.Now:HH:mm:ss}";
            txtStatusPolaczenia.Text = "🟢 Połączono";
        }

        private void UpdateDashboard(List<RejestracjaModel> data)
        {
            var dzisiaj = _wszystkieRejestracje.Where(r => r.DataCzas.Date == DateTime.Today).ToList();
            var wejsciaDzisiaj = dzisiaj.Where(r => r.TypInt == 1).ToList();
            var wyjsciaDzisiaj = dzisiaj.Where(r => r.TypInt == 0).ToList();

            // Obecni teraz
            var wejsciaDict = wejsciaDzisiaj.GroupBy(r => r.PracownikId).ToDictionary(g => g.Key, g => g.Max(r => r.DataCzas));
            var wyjsciaDict = wyjsciaDzisiaj.GroupBy(r => r.PracownikId).ToDictionary(g => g.Key, g => g.Max(r => r.DataCzas));
            
            int obecni = 0;
            foreach (var w in wejsciaDict)
            {
                if (!wyjsciaDict.ContainsKey(w.Key) || wyjsciaDict[w.Key] < w.Value)
                    obecni++;
            }

            txtDashObecni.Text = obecni.ToString();
            txtDashWejscia.Text = wejsciaDzisiaj.Select(r => r.PracownikId).Distinct().Count().ToString();
            txtDashWyjscia.Text = wyjsciaDzisiaj.Select(r => r.PracownikId).Distinct().Count().ToString();

            // Ostatnie wejście/wyjście
            var ostatnieWejscie = wejsciaDzisiaj.OrderByDescending(r => r.DataCzas).FirstOrDefault();
            var ostatnieWyjscie = wyjsciaDzisiaj.OrderByDescending(r => r.DataCzas).FirstOrDefault();
            txtDashWejsciaGodzina.Text = ostatnieWejscie != null ? $"Ostatnie: {ostatnieWejscie.DataCzas:HH:mm}" : "Ostatnie: --:--";
            txtDashWyjsciaGodzina.Text = ostatnieWyjscie != null ? $"Ostatnie: {ostatnieWyjscie.DataCzas:HH:mm}" : "Ostatnie: --:--";

            // Suma godzin
            double sumaGodzin = 0;
            var pracownicyDzis = dzisiaj.Select(r => r.PracownikId).Distinct().Count();
            var byPracownik = dzisiaj.GroupBy(r => r.PracownikId);
            foreach (var p in byPracownik)
            {
                var we = p.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).FirstOrDefault();
                var wy = p.Where(r => r.TypInt == 0).OrderByDescending(r => r.DataCzas).FirstOrDefault();
                if (we != null && wy != null && wy.DataCzas > we.DataCzas)
                    sumaGodzin += (wy.DataCzas - we.DataCzas).TotalHours;
                else if (we != null)
                    sumaGodzin += (DateTime.Now - we.DataCzas).TotalHours;
            }
            txtDashGodziny.Text = $"{sumaGodzin:N0}h";
            txtDashGodzinySrednia.Text = pracownicyDzis > 0 ? $"Śr./osobę: {sumaGodzin / pracownicyDzis:N1}h" : "Śr./osobę: 0h";

            // Alerty
            int alertyCount = 0;
            // TODO: policzyć alerty
            txtDashAlerty.Text = alertyCount.ToString();
            txtDashAlertyTyp.Text = alertyCount == 0 ? "Brak problemów" : $"{alertyCount} problemów";

            // Obecność wg lokalizacji
            var obecniPracownicy = new HashSet<int>();
            foreach (var w in wejsciaDict)
            {
                if (!wyjsciaDict.ContainsKey(w.Key) || wyjsciaDict[w.Key] < w.Value)
                    obecniPracownicy.Add(w.Key);
            }

            var obecniRej = dzisiaj.Where(r => obecniPracownicy.Contains(r.PracownikId)).ToList();
            
            txtDashProdukcja.Text = obecniRej.Count(r => r.Grupa?.ToUpper().Contains("PRODUKCJ") == true || 
                                                         r.Grupa?.ToUpper().Contains("GURAVO") == true).ToString();
            txtDashCzysta.Text = obecniRej.Count(r => r.Grupa?.ToUpper().Contains("CZYST") == true).ToString();
            txtDashBrudna.Text = obecniRej.Count(r => r.Grupa?.ToUpper().Contains("BRUDN") == true).ToString();
            txtDashMyjka.Text = obecniRej.Count(r => r.Grupa?.ToUpper().Contains("MYJK") == true).ToString();
            txtDashMechanicy.Text = obecniRej.Count(r => r.Grupa?.ToUpper().Contains("MECHAN") == true).ToString();
            txtDashBiuro.Text = obecniRej.Count(r => r.Grupa?.ToUpper().Contains("BIUR") == true).ToString();

            // Ostatnie wejścia/wyjścia
            gridDashWejscia.ItemsSource = wejsciaDzisiaj
                .OrderByDescending(r => r.DataCzas)
                .Take(10)
                .Select(r => new { Godzina = r.DataCzas.ToString("HH:mm"), r.Pracownik, r.Grupa })
                .ToList();

            gridDashWyjscia.ItemsSource = wyjsciaDzisiaj
                .OrderByDescending(r => r.DataCzas)
                .Take(10)
                .Select(r => new { Godzina = r.DataCzas.ToString("HH:mm"), r.Pracownik, r.Grupa })
                .ToList();
        }

        private void UpdateRejestracje(List<RejestracjaModel> data)
        {
            var viewData = data.Select(r => new
            {
                r.DataCzas,
                Godzina = r.DataCzas.ToString("HH:mm:ss"),
                r.Typ,
                r.Pracownik,
                r.Grupa,
                r.PunktDostepu,
                r.TypPunktu
            }).ToList();

            gridRejestracje.ItemsSource = viewData;
        }

        private void UpdateGodzinyPracy(List<RejestracjaModel> data)
        {
            var grouped = data
                .GroupBy(r => new { r.PracownikId, Data = r.DataCzas.Date })
                .Select(g =>
                {
                    var wejscia = g.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).ToList();
                    var wyjscia = g.Where(r => r.TypInt == 0).OrderBy(r => r.DataCzas).ToList();

                    var pierwszeWejscie = wejscia.FirstOrDefault()?.DataCzas;
                    var ostatnieWyjscie = wyjscia.LastOrDefault()?.DataCzas;

                    TimeSpan czasPracy = TimeSpan.Zero;
                    TimeSpan czasPrzerw = TimeSpan.Zero;
                    int liczbaPrzerw = 0;

                    // Oblicz czas pracy i przerwy
                    if (pierwszeWejscie.HasValue && ostatnieWyjscie.HasValue && ostatnieWyjscie > pierwszeWejscie)
                    {
                        czasPracy = ostatnieWyjscie.Value - pierwszeWejscie.Value;

                        // Oblicz przerwy (wyjście -> następne wejście)
                        for (int i = 0; i < wyjscia.Count; i++)
                        {
                            var nastepneWejscie = wejscia.FirstOrDefault(w => w.DataCzas > wyjscia[i].DataCzas);
                            if (nastepneWejscie != null)
                            {
                                var przerwa = nastepneWejscie.DataCzas - wyjscia[i].DataCzas;
                                if (przerwa.TotalMinutes > 2 && przerwa.TotalHours < 4) // Rozsądna przerwa
                                {
                                    czasPrzerw += przerwa;
                                    liczbaPrzerw++;
                                }
                            }
                        }
                    }

                    var czasEfektywny = czasPracy - czasPrzerw;
                    var nadgodzinyTs = czasEfektywny.TotalHours > 8
                        ? TimeSpan.FromHours(czasEfektywny.TotalHours - 8)
                        : TimeSpan.Zero;

                    string status = "OK";
                    if (!ostatnieWyjscie.HasValue && g.Key.Data == DateTime.Today)
                        status = "🟢 Na terenie";
                    else if (!ostatnieWyjscie.HasValue)
                        status = "⚠️ Brak wyjścia";
                    else if (czasEfektywny.TotalHours > 10)
                        status = "⚠️ Długa zmiana";

                    return new
                    {
                        PracownikId = g.Key.PracownikId,
                        Data = g.Key.Data,
                        DzienTygodnia = g.Key.Data.ToString("ddd"),
                        Pracownik = g.First().Pracownik,
                        Grupa = g.First().Grupa,
                        PierwszeWejscie = pierwszeWejscie?.ToString("HH:mm") ?? "-",
                        OstatnieWyjscie = ostatnieWyjscie?.ToString("HH:mm") ?? "-",
                        CzasPracy = FormatTimeSpan(czasPracy),
                        Przerwy = FormatTimeSpan(czasPrzerw),
                        CzasEfektywny = FormatTimeSpan(czasEfektywny),
                        Nadgodziny = nadgodzinyTs > TimeSpan.Zero ? $"+{(int)nadgodzinyTs.TotalHours}:{nadgodzinyTs.Minutes:D2}" : "-",
                        Status = status,
                        CzasPracyTS = czasPracy,
                        CzasEfektywnyTS = czasEfektywny,
                        NadgodzinyTS = nadgodzinyTs,
                        PierwszeWejscieDateTime = pierwszeWejscie,
                        OstatnieWyjscieDateTime = ostatnieWyjscie
                    };
                })
                .OrderByDescending(x => x.Data)
                .ThenBy(x => x.Pracownik)
                .ToList();

            gridGodzinyPracy.ItemsSource = grouped;
            UpdateGodzinyPracyPodsumowanie(grouped);
        }

        private void UpdateGodzinyPracyPodsumowanie(dynamic grouped)
        {
            try
            {
                var items = new List<dynamic>();
                foreach (var item in grouped)
                    items.Add(item);

                if (items.Count == 0)
                {
                    txtGPNazwisko.Text = "Brak danych";
                    txtGPDzial.Text = "";
                    txtGPAgencja.Text = "";
                    txtGPSrEfektywny.Text = "-";
                    txtGPSrCzasPracy.Text = "-";
                    txtGPDniPracy.Text = "0";
                    txtGPSumaGodzin.Text = "0h";
                    txtGPNadgodziny.Text = "0h";
                    txtGPSrWejscieWyjscie.Text = "-";
                    return;
                }

                // Sprawdź liczbę unikalnych pracowników
                var pracownikIds = new HashSet<int>();
                string pierwszyPracownik = "";
                string pierwszyDzial = "";
                foreach (var item in items)
                {
                    int pid = item.PracownikId;
                    pracownikIds.Add(pid);
                    if (string.IsNullOrEmpty(pierwszyPracownik))
                    {
                        pierwszyPracownik = item.Pracownik;
                        pierwszyDzial = item.Grupa;
                    }
                }

                if (pracownikIds.Count == 1)
                {
                    txtGPNazwisko.Text = pierwszyPracownik;
                    txtGPDzial.Text = pierwszyDzial;
                    txtGPAgencja.Text = "";
                }
                else
                {
                    txtGPNazwisko.Text = "Wszyscy pracownicy";
                    txtGPDzial.Text = $"{pracownikIds.Count} pracowników";
                    txtGPAgencja.Text = "";
                }

                // Oblicz statystyki z surowych TimeSpan
                int dniPracy = items.Count;
                TimeSpan sumaEfektywny = TimeSpan.Zero;
                TimeSpan sumaCzasPracy = TimeSpan.Zero;
                TimeSpan sumaNadgodziny = TimeSpan.Zero;
                double sumaMinutWejscie = 0;
                double sumaMinutWyjscie = 0;
                int countWejscie = 0;
                int countWyjscie = 0;

                foreach (var item in items)
                {
                    TimeSpan efektywnyTs = item.CzasEfektywnyTS;
                    TimeSpan pracyTs = item.CzasPracyTS;
                    TimeSpan nadgodzinyTs = item.NadgodzinyTS;
                    DateTime? wejscie = item.PierwszeWejscieDateTime;
                    DateTime? wyjscie = item.OstatnieWyjscieDateTime;

                    sumaEfektywny += efektywnyTs;
                    sumaCzasPracy += pracyTs;
                    sumaNadgodziny += nadgodzinyTs;

                    if (wejscie.HasValue)
                    {
                        sumaMinutWejscie += wejscie.Value.Hour * 60 + wejscie.Value.Minute;
                        countWejscie++;
                    }
                    if (wyjscie.HasValue)
                    {
                        sumaMinutWyjscie += wyjscie.Value.Hour * 60 + wyjscie.Value.Minute;
                        countWyjscie++;
                    }
                }

                // Średni czas efektywny
                var srEfektywny = TimeSpan.FromTicks(sumaEfektywny.Ticks / dniPracy);
                txtGPSrEfektywny.Text = FormatTimeSpan(srEfektywny);

                // Średni czas pracy
                var srCzasPracy = TimeSpan.FromTicks(sumaCzasPracy.Ticks / dniPracy);
                txtGPSrCzasPracy.Text = FormatTimeSpan(srCzasPracy);

                // Dni pracy
                txtGPDniPracy.Text = dniPracy.ToString();

                // Suma godzin efektywnych
                int sumaH = (int)sumaEfektywny.TotalHours;
                int sumaM = sumaEfektywny.Minutes;
                txtGPSumaGodzin.Text = $"{sumaH}h {sumaM}m";

                // Nadgodziny
                int nadH = (int)sumaNadgodziny.TotalHours;
                int nadM = sumaNadgodziny.Minutes;
                txtGPNadgodziny.Text = nadH > 0 || nadM > 0 ? $"{nadH}h {nadM}m" : "0h";

                // Średnie wejście / wyjście
                if (countWejscie > 0 && countWyjscie > 0)
                {
                    int srWejscieMin = (int)(sumaMinutWejscie / countWejscie);
                    int srWyjscieMin = (int)(sumaMinutWyjscie / countWyjscie);
                    string srWejscie = $"{srWejscieMin / 60:D2}:{srWejscieMin % 60:D2}";
                    string srWyjscie = $"{srWyjscieMin / 60:D2}:{srWyjscieMin % 60:D2}";
                    txtGPSrWejscieWyjscie.Text = $"{srWejscie} / {srWyjscie}";
                }
                else
                {
                    txtGPSrWejscieWyjscie.Text = "-";
                }
            }
            catch
            {
                txtGPSrEfektywny.Text = "-";
                txtGPSrCzasPracy.Text = "-";
                txtGPDniPracy.Text = "0";
                txtGPSumaGodzin.Text = "0h";
                txtGPNadgodziny.Text = "0h";
                txtGPSrWejscieWyjscie.Text = "-";
            }
        }

        private void UpdateObecni(List<RejestracjaModel> data)
        {
            var dzisiaj = data.Where(r => r.DataCzas.Date == DateTime.Today).ToList();
            
            var obecni = dzisiaj
                .GroupBy(r => r.PracownikId)
                .Select(g =>
                {
                    var ostatnia = g.OrderByDescending(r => r.DataCzas).First();
                    var wejscie = g.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).FirstOrDefault()?.DataCzas;
                    
                    // Sprawdź czy jest na terenie (ostatnia rejestracja to wejście)
                    var czyObecny = g.OrderByDescending(r => r.DataCzas).First().TypInt == 1;
                    
                    if (!czyObecny) return null;

                    return new
                    {
                        Pracownik = ostatnia.Pracownik,
                        Grupa = ostatnia.Grupa,
                        GodzinaWejscia = wejscie?.ToString("HH:mm") ?? "-",
                        CzasObecnosci = wejscie.HasValue ? FormatTimeSpan(DateTime.Now - wejscie.Value) : "-",
                        PunktWejscia = ostatnia.PunktDostepu
                    };
                })
                .Where(x => x != null)
                .OrderBy(x => x.Pracownik)
                .ToList();

            gridObecni.ItemsSource = obecni;
            
            // Aktualizuj statystyki
            txtObecniTeraz.Text = obecni.Count.ToString();
            var wejsciaDzis = dzisiaj.Count(r => r.TypInt == 1);
            var wyjsciaDzis = dzisiaj.Count(r => r.TypInt == 0);
            txtWejsciaDzis.Text = wejsciaDzis.ToString();
            txtWyjsciaDzis.Text = wyjsciaDzis.ToString();
        }

        private void UpdatePodsumowanie(List<RejestracjaModel> data)
        {
            var podsumowanie = data
                .GroupBy(r => r.Grupa)
                .Select(g =>
                {
                    var pracownicy = g.Select(r => r.PracownikId).Distinct().Count();
                    var liczbaWejsc = g.Count(r => r.TypInt == 1);
                    var liczbaWyjsc = g.Count(r => r.TypInt == 0);
                    
                    // Oblicz sumy godzin
                    double sumaGodzin = 0;
                    int braki = 0;
                    int problemy = 0;

                    var byPracownikDzien = g.GroupBy(r => new { r.PracownikId, Data = r.DataCzas.Date });
                    foreach (var pd in byPracownikDzien)
                    {
                        var wejscia = pd.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).ToList();
                        var wyjscia = pd.Where(r => r.TypInt == 0).OrderBy(r => r.DataCzas).ToList();

                        if (wejscia.Any() && wyjscia.Any())
                        {
                            var czas = (wyjscia.Last().DataCzas - wejscia.First().DataCzas).TotalHours;
                            sumaGodzin += Math.Max(0, czas);
                            if (czas > 10) problemy++;
                        }
                        else if (wejscia.Any() && !wyjscia.Any() && pd.Key.Data < DateTime.Today)
                        {
                            braki++;
                        }
                    }

                    string status = braki == 0 && problemy == 0 ? "✅ OK" : 
                                   braki > 0 ? $"⚠️ Brak wyjść: {braki}" : 
                                   $"⚠️ Przekroczenia: {problemy}";

                    return new
                    {
                        Grupa = g.Key,
                        LiczbaPracownikow = pracownicy,
                        SumaGodzin = sumaGodzin,
                        SredniaNaOsobe = pracownicy > 0 ? sumaGodzin / pracownicy : 0,
                        LiczbaWejsc = liczbaWejsc,
                        LiczbaWyjsc = liczbaWyjsc,
                        Problemy = braki + problemy,
                        Status = status
                    };
                })
                .OrderByDescending(x => x.SumaGodzin)
                .ToList();

            // gridPodsumowanie nie istnieje - dane podsumowania są w Dashboard
            // Karty podsumowania są teraz w Dashboard
            // Aktualizacja w UpdateDashboard
        }

        private void UpdateAlerty(List<RejestracjaModel> data)
        {
            var alerty = new List<object>();

            var byPracownikDzien = data.GroupBy(r => new { r.PracownikId, r.Pracownik, r.Grupa, Data = r.DataCzas.Date });
            
            foreach (var pd in byPracownikDzien)
            {
                var wejscia = pd.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).ToList();
                var wyjscia = pd.Where(r => r.TypInt == 0).OrderBy(r => r.DataCzas).ToList();

                // Brak wyjścia
                if (wejscia.Any() && !wyjscia.Any() && pd.Key.Data < DateTime.Today)
                {
                    alerty.Add(new
                    {
                        TypAlertu = "⚠️ Brak wyjścia",
                        Priorytet = "Wysoki",
                        Pracownik = pd.Key.Pracownik,
                        Grupa = pd.Key.Grupa,
                        Data = pd.Key.Data,
                        Opis = "Brak zarejestrowanego wyjścia"
                    });
                }

                // Długa zmiana
                if (wejscia.Any() && wyjscia.Any())
                {
                    var czas = (wyjscia.Last().DataCzas - wejscia.First().DataCzas).TotalHours;
                    if (czas > 12)
                    {
                        alerty.Add(new
                        {
                            TypAlertu = "🔴 Przekroczenie 12h",
                            Priorytet = "Krytyczny",
                            Pracownik = pd.Key.Pracownik,
                            Grupa = pd.Key.Grupa,
                            Data = pd.Key.Data,
                            Opis = $"Czas pracy: {czas:N1}h (limit: 12h)"
                        });
                    }
                    else if (czas > 10)
                    {
                        alerty.Add(new
                        {
                            TypAlertu = "🟡 Długa zmiana",
                            Priorytet = "Średni",
                            Pracownik = pd.Key.Pracownik,
                            Grupa = pd.Key.Grupa,
                            Data = pd.Key.Data,
                            Opis = $"Czas pracy: {czas:N1}h"
                        });
                    }
                }

                // Spóźnienie
                if (wejscia.Any())
                {
                    var pierwszeWejscie = wejscia.First().DataCzas;
                    if (pierwszeWejscie.Hour >= 6 && pierwszeWejscie.Minute > 15)
                    {
                        alerty.Add(new
                        {
                            TypAlertu = "⏰ Spóźnienie",
                            Priorytet = "Niski",
                            Pracownik = pd.Key.Pracownik,
                            Grupa = pd.Key.Grupa,
                            Data = pd.Key.Data,
                            Opis = $"Wejście o {pierwszeWejscie:HH:mm}"
                        });
                    }
                }
            }

            gridAlertyPodglad.ItemsSource = alerty.OrderByDescending(a => ((dynamic)a).Priorytet).ToList();
        }

        private void UpdateRanking(List<RejestracjaModel> data)
        {
            var ranking = data
                .GroupBy(r => new { r.PracownikId, r.Pracownik, r.Grupa })
                .Select(g =>
                {
                    var dniPracy = g.Select(r => r.DataCzas.Date).Distinct().Count();
                    double sumaGodzin = 0;
                    double sumaPrzerw = 0;
                    int spoznienia = 0;

                    var byDzien = g.GroupBy(r => r.DataCzas.Date);
                    foreach (var dzien in byDzien)
                    {
                        var wejscia = dzien.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).ToList();
                        var wyjscia = dzien.Where(r => r.TypInt == 0).OrderBy(r => r.DataCzas).ToList();

                        if (wejscia.Any() && wyjscia.Any())
                        {
                            var czas = (wyjscia.Last().DataCzas - wejscia.First().DataCzas).TotalHours;
                            sumaGodzin += Math.Max(0, czas);
                        }

                        if (wejscia.Any() && wejscia.First().DataCzas.Hour >= 6 && wejscia.First().DataCzas.Minute > 5)
                            spoznienia++;
                    }

                    double punktualnosc = dniPracy > 0 ? ((dniPracy - spoznienia) / (double)dniPracy) * 100 : 100;
                    string ocena = punktualnosc >= 95 ? "⭐⭐⭐" : punktualnosc >= 85 ? "⭐⭐" : punktualnosc >= 70 ? "⭐" : "❌";

                    return new
                    {
                        PracownikId = g.Key.PracownikId,
                        Pracownik = g.Key.Pracownik,
                        Grupa = g.Key.Grupa,
                        DniPracy = dniPracy,
                        SumaGodzin = sumaGodzin,
                        SredniaGodzin = dniPracy > 0 ? sumaGodzin / dniPracy : 0,
                        SumaPrzerw = sumaPrzerw,
                        Punktualnosc = $"{punktualnosc:N0}%",
                        Ocena = ocena
                    };
                })
                .OrderByDescending(x => x.SumaGodzin)
                .Select((x, i) => new
                {
                    Pozycja = i + 1,
                    x.Pracownik,
                    x.Grupa,
                    x.DniPracy,
                    x.SumaGodzin,
                    x.SredniaGodzin,
                    x.SumaPrzerw,
                    x.Punktualnosc,
                    x.Ocena
                })
                .ToList();

            gridRanking.ItemsSource = ranking;
        }

        private void UpdateNadgodziny(List<RejestracjaModel> data)
        {
            var nadgodziny = data
                .GroupBy(r => new { r.PracownikId, r.Pracownik, r.Grupa })
                .Select(g =>
                {
                    double dzis = 0, tydzien = 0, miesiac = 0, rok = 0;
                    var teraz = DateTime.Now;
                    var poczatekTygodnia = teraz.AddDays(-(int)teraz.DayOfWeek + 1);
                    var poczatekMiesiaca = new DateTime(teraz.Year, teraz.Month, 1);
                    var poczatekRoku = new DateTime(teraz.Year, 1, 1);

                    var byDzien = g.GroupBy(r => r.DataCzas.Date);
                    foreach (var dzien in byDzien)
                    {
                        var wejscia = dzien.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).ToList();
                        var wyjscia = dzien.Where(r => r.TypInt == 0).OrderBy(r => r.DataCzas).ToList();

                        if (wejscia.Any() && wyjscia.Any())
                        {
                            var godziny = (wyjscia.Last().DataCzas - wejscia.First().DataCzas).TotalHours;
                            var nadg = Math.Max(0, godziny - 8);

                            if (dzien.Key == teraz.Date) dzis += nadg;
                            if (dzien.Key >= poczatekTygodnia) tydzien += nadg;
                            if (dzien.Key >= poczatekMiesiaca) miesiac += nadg;
                            if (dzien.Key >= poczatekRoku) rok += nadg;
                        }
                    }

                    double procent = (rok / 150.0) * 100;
                    string status = procent >= 100 ? "🔴 PRZEKROCZONY" :
                                   procent >= 80 ? "🟡 Zbliża się" :
                                   procent >= 50 ? "🟠 Połowa" : "🟢 OK";

                    return new
                    {
                        Pracownik = g.Key.Pracownik,
                        Grupa = g.Key.Grupa,
                        NadgodzinyDzien = dzis,
                        NadgodzinyTydzien = tydzien,
                        NadgodzinyMiesiac = miesiac,
                        NadgodzinyRok = rok,
                        ProcentLimitu = procent,
                        StatusLimitu = status
                    };
                })
                .Where(x => x.NadgodzinyRok > 0)
                .OrderByDescending(x => x.NadgodzinyRok)
                .ToList();

            // gridNadgodzinyPodglad.ItemsSource = nadgodziny; // Zakładka usunięta
        }

        private void UpdatePunktualnosc(List<RejestracjaModel> data)
        {
            var punktualnosc = data
                .GroupBy(r => new { r.PracownikId, r.Pracownik, r.Grupa })
                .Select(g =>
                {
                    int dniPracy = 0, spoznienia = 0, wczesneWyjscia = 0;
                    int sumaSpoznienMin = 0;

                    var byDzien = g.GroupBy(r => r.DataCzas.Date);
                    foreach (var dzien in byDzien)
                    {
                        var wejscia = dzien.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).ToList();
                        var wyjscia = dzien.Where(r => r.TypInt == 0).OrderBy(r => r.DataCzas).ToList();

                        if (wejscia.Any())
                        {
                            dniPracy++;
                            var pierwszeWejscie = wejscia.First().DataCzas;
                            // Przyjmujemy że zmiana zaczyna się o 6:00
                            if (pierwszeWejscie.Hour >= 6)
                            {
                                var spoznienie = (int)(pierwszeWejscie - dzien.Key.AddHours(6)).TotalMinutes;
                                if (spoznienie > 5)
                                {
                                    spoznienia++;
                                    sumaSpoznienMin += spoznienie;
                                }
                            }
                        }

                        // Wczesne wyjście - przed 14:00 przy normalnej zmianie
                        if (wyjscia.Any())
                        {
                            var ostatnieWyjscie = wyjscia.Last().DataCzas;
                            if (ostatnieWyjscie.Hour < 14 && wejscia.Any())
                            {
                                wczesneWyjscia++;
                            }
                        }
                    }

                    double procent = dniPracy > 0 ? ((dniPracy - spoznienia) / (double)dniPracy) * 100 : 100;
                    string ocena = procent >= 95 ? "⭐⭐⭐ Wzorowa" :
                                  procent >= 85 ? "⭐⭐ Dobra" :
                                  procent >= 70 ? "⭐ Do poprawy" : "❌ Słaba";

                    return new
                    {
                        Pracownik = g.Key.Pracownik,
                        Grupa = g.Key.Grupa,
                        DniPracy = dniPracy,
                        Spoznienia = spoznienia,
                        SumaSpoznienMin = sumaSpoznienMin,
                        WczesniejszeWyjscia = wczesneWyjscia,
                        ProcentPunktualnosci = procent,
                        Trend = spoznienia == 0 ? "📈" : spoznienia <= 2 ? "➡️" : "📉",
                        Ocena = ocena
                    };
                })
                .OrderByDescending(x => x.ProcentPunktualnosci)
                .ToList();

            gridPunktualnosc.ItemsSource = punktualnosc;
        }

        private void UpdateNieobecnosci(List<RejestracjaModel> data)
        {
            // Znajdź dni bez rejestracji dla każdego pracownika
            var nieobecnosci = new List<object>();
            
            DateTime dataOd = dpOd.SelectedDate ?? DateTime.Today.AddDays(-30);
            DateTime dataDo = dpDo.SelectedDate ?? DateTime.Today;

            var pracownicyZDanymi = data.Select(r => new { r.PracownikId, r.Pracownik, r.Grupa }).Distinct().ToList();
            var dniZRejestracjami = data.GroupBy(r => new { r.PracownikId, Data = r.DataCzas.Date })
                                        .Select(g => g.Key).ToHashSet();

            foreach (var prac in pracownicyZDanymi)
            {
                for (var d = dataOd; d <= dataDo; d = d.AddDays(1))
                {
                    // Pomijaj weekendy
                    if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                        continue;

                    // Pomijaj przyszłość
                    if (d > DateTime.Today)
                        continue;

                    if (!dniZRejestracjami.Contains(new { prac.PracownikId, Data = d }))
                    {
                        nieobecnosci.Add(new
                        {
                            Data = d,
                            Pracownik = prac.Pracownik,
                            Grupa = prac.Grupa,
                            TypNieobecnosci = "❓ Nieusprawiedliwiona",
                            Status = "Do wyjaśnienia",
                            Uwagi = ""
                        });
                    }
                }
            }

            gridNieobecnosci.ItemsSource = nieobecnosci.OrderByDescending(n => ((dynamic)n).Data).ToList();

            // Statystyki
            int niusp = nieobecnosci.Count;
            txtNieobecnosciNieusp.Text = niusp.ToString();
            txtNieobecnosciChoroba.Text = "0"; // Wymagałoby integracji z danymi kadrowymi
            txtNieobecnosciUrlop.Text = "0";
        }

        private void UpdatePorownanie(List<RejestracjaModel> data)
        {
            // Porównanie wymaga danych z dwóch okresów
            // Na razie pokazujemy bieżące dane jako M1, M2 jako placeholder
            var porownanie = data
                .GroupBy(r => r.Grupa)
                .Select(g =>
                {
                    var pracownicy = g.Select(r => r.PracownikId).Distinct().Count();
                    double sumaGodzin = 0;

                    var byPracownikDzien = g.GroupBy(r => new { r.PracownikId, Data = r.DataCzas.Date });
                    foreach (var pd in byPracownikDzien)
                    {
                        var wejscia = pd.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).ToList();
                        var wyjscia = pd.Where(r => r.TypInt == 0).OrderBy(r => r.DataCzas).ToList();

                        if (wejscia.Any() && wyjscia.Any())
                        {
                            var czas = (wyjscia.Last().DataCzas - wejscia.First().DataCzas).TotalHours;
                            sumaGodzin += Math.Max(0, czas);
                        }
                    }

                    return new
                    {
                        Grupa = g.Key,
                        GodzinyM1 = sumaGodzin,
                        GodzinyM2 = 0.0, // Wymaga załadowania danych z poprzedniego miesiąca
                        Zmiana = "-",
                        PracownicyM1 = pracownicy,
                        PracownicyM2 = 0,
                        Trend = "📊 Załaduj porównanie"
                    };
                })
                .OrderByDescending(x => x.GodzinyM1)
                .ToList();

            gridPorownanie.ItemsSource = porownanie;
        }

        #endregion

        #region Pomocnicze

        private string FormatTimeSpan(TimeSpan ts)
        {
            if (ts.TotalHours < 0) return "-";
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}";
        }

        #endregion

        #region Event Handlers

        private void DpOd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadAllData();
        }

        private void DpDo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) LoadAllData();
        }

        private void ChkTylkoDzisiaj_Changed(object sender, RoutedEventArgs e)
        {
            if (chkTylkoDzisiaj.IsChecked == true)
            {
                dpOd.SelectedDate = DateTime.Today;
                dpDo.SelectedDate = DateTime.Today;
            }
        }

        private void BtnWczoraj_Click(object sender, RoutedEventArgs e)
        {
            chkTylkoDzisiaj.IsChecked = false;
            dpOd.SelectedDate = DateTime.Today.AddDays(-1);
            dpDo.SelectedDate = DateTime.Today.AddDays(-1);
        }

        private void BtnTydzien_Click(object sender, RoutedEventArgs e)
        {
            chkTylkoDzisiaj.IsChecked = false;
            dpOd.SelectedDate = DateTime.Today.AddDays(-7);
            dpDo.SelectedDate = DateTime.Today;
        }

        private void BtnMiesiac_Click(object sender, RoutedEventArgs e)
        {
            chkTylkoDzisiaj.IsChecked = false;
            dpOd.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpDo.SelectedDate = DateTime.Today;
        }

        private void CmbGrupa_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) UpdateAllViews();
        }

        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded) UpdateAllViews();
        }

        private void BtnWyczyscSzukaj_Click(object sender, RoutedEventArgs e)
        {
            txtSzukaj.Text = "";
        }

        private void BtnOdswiez_Click(object sender, RoutedEventArgs e)
        {
            loadingOverlay.Show("Odświeżanie danych...");
            try
            {
                LoadGrupy();
                LoadPracownicy();
                LoadAllData();
            }
            finally
            {
                loadingOverlay.Hide();
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"rejestracje_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Data;Godzina;Typ;Pracownik;Dział;Punkt dostępu");

                    foreach (var r in _wszystkieRejestracje)
                    {
                        sb.AppendLine($"{r.DataCzas:yyyy-MM-dd};{r.DataCzas:HH:mm:ss};{r.Typ};{r.Pracownik};{r.Grupa};{r.PunktDostepu}");
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Wyeksportowano {_wszystkieRejestracje.Count} rekordów.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CmbMiesiac_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void CmbRok_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        
        private void BtnGenerujRaportMiesieczny_Click(object sender, RoutedEventArgs e) 
        {
            GenerujRaportMiesieczny();
        }

        private void GenerujRaportMiesieczny()
        {
            if (!IsLoaded || gridRaportMiesieczny == null || cmbMiesiac.SelectedIndex < 0 || cmbRok.SelectedItem == null) return;

            try
            {
                int miesiac = cmbMiesiac.SelectedIndex + 1;
                int rok = (int)cmbRok.SelectedItem;
                var pierwszyDzien = new DateTime(rok, miesiac, 1);
                var ostatniDzien = pierwszyDzien.AddMonths(1);

                // Pobierz rejestracje bezpośrednio z bazy dla wybranego miesiąca
                var rejestracjeMiesiac = new List<RejestracjaModel>();

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = @"
                        SELECT 
                            KDINAR_REGISTRTN_DATETIME,
                            KDINAR_REGISTRTN_TYPE,
                            KDINAR_EMPLOYEE_ID,
                            KDINAR_EMPLOYEE_NAME,
                            KDINAR_EMPLOYEE_SURNAME,
                            KDINAR_EMPLOYEE_GROUP_ID,
                            KDINAR_ACCESS_POINT_NAME
                        FROM V_KDINAR_ALL_REGISTRATIONS
                        WHERE KDINAR_REGISTRTN_DATETIME >= @DataOd 
                          AND KDINAR_REGISTRTN_DATETIME < @DataDo
                          AND KDINAR_EMPLOYEE_ID IS NOT NULL
                        ORDER BY KDINAR_EMPLOYEE_ID, KDINAR_REGISTRTN_DATETIME";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", pierwszyDzien);
                        cmd.Parameters.AddWithValue("@DataDo", ostatniDzien);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var punktDostepu = reader.IsDBNull(6) ? "" : reader.GetString(6);
                                var typZBazy = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                                int typInt = OkreslTypWejsciaWyjscia(punktDostepu, typZBazy);

                                var reg = new RejestracjaModel
                                {
                                    DataCzas = reader.GetDateTime(0),
                                    TypInt = typInt,
                                    PracownikId = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                    Pracownik = $"{(reader.IsDBNull(4) ? "" : reader.GetString(4))} {(reader.IsDBNull(3) ? "" : reader.GetString(3))}".Trim(),
                                    GrupaId = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
                                };

                                var grupa = _grupy.FirstOrDefault(g => g.Id == reg.GrupaId);
                                reg.Grupa = grupa?.Nazwa ?? "Brak działu";

                                rejestracjeMiesiac.Add(reg);
                            }
                        }
                    }
                }

                // Filtruj po wybranym dziale (jeśli wybrano)
                if (cmbGrupa.SelectedIndex > 0)
                {
                    var wybranaGrupa = cmbGrupa.SelectedItem as GrupaModel;
                    if (wybranaGrupa != null)
                        rejestracjeMiesiac = rejestracjeMiesiac.Where(r => r.GrupaId == wybranaGrupa.Id).ToList();
                }

                var raport = rejestracjeMiesiac
                    .GroupBy(r => new { r.PracownikId, r.Pracownik, r.Grupa })
                    .Select(g =>
                    {
                        double sumaGodzin = 0;
                        int dniPracy = 0;
                        int brakiOdbic = 0;
                        var uwagi = new List<string>();

                        var byDzien = g.GroupBy(r => r.DataCzas.Date).OrderBy(d => d.Key);
                        foreach (var dzien in byDzien)
                        {
                            var wejscia = dzien.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).ToList();
                            var wyjscia = dzien.Where(r => r.TypInt == 0).OrderBy(r => r.DataCzas).ToList();

                            if (wejscia.Any())
                            {
                                dniPracy++;
                                
                                if (wyjscia.Any())
                                {
                                    var pierwszeWejscie = wejscia.First().DataCzas;
                                    var ostatnieWyjscie = wyjscia.Last().DataCzas;
                                    var godzinyDzien = (ostatnieWyjscie - pierwszeWejscie).TotalHours;
                                    sumaGodzin += godzinyDzien;
                                }
                                else
                                {
                                    brakiOdbic++;
                                    uwagi.Add($"{dzien.Key:dd.MM} brak wyjścia");
                                }
                            }
                            
                            if (wyjscia.Any() && !wejscia.Any())
                            {
                                brakiOdbic++;
                                uwagi.Add($"{dzien.Key:dd.MM} brak wejścia");
                            }
                        }

                        return new
                        {
                            PracownikId = g.Key.PracownikId,
                            Pracownik = g.Key.Pracownik,
                            Grupa = g.Key.Grupa,
                            DniPracy = dniPracy,
                            SumaGodzin = sumaGodzin,
                            BrakiOdbic = brakiOdbic,
                            Uwagi = uwagi.Any() ? string.Join("; ", uwagi) : "✅ OK"
                        };
                    })
                    .OrderBy(x => x.Grupa)
                    .ThenBy(x => x.Pracownik)
                    .ToList();

                gridRaportMiesieczny.ItemsSource = raport;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd generowania raportu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDrukujRaport_Click(object sender, RoutedEventArgs e) 
        {
            if (gridRaportMiesieczny.ItemsSource == null)
            {
                MessageBox.Show("Najpierw wygeneruj raport.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int miesiac = cmbMiesiac.SelectedIndex + 1;
                int rok = (int)cmbRok.SelectedItem;
                string miesiacNazwa = cmbMiesiac.SelectedItem?.ToString() ?? "";
                
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() != true) return;

                // Tworzenie dokumentu do druku
                var document = new System.Windows.Documents.FlowDocument();
                document.PageWidth = printDialog.PrintableAreaWidth;
                document.PageHeight = printDialog.PrintableAreaHeight;
                document.PagePadding = new Thickness(40);
                document.ColumnWidth = document.PageWidth;
                document.FontFamily = new FontFamily("Segoe UI");

                // Nagłówek
                var header = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"RAPORT MIESIĘCZNY - {miesiacNazwa.ToUpper()} {rok}"))
                {
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                document.Blocks.Add(header);

                var subHeader = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"Ubojnia Drobiu Piórkowscy • Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}"))
                {
                    FontSize = 10,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 20)
                };
                document.Blocks.Add(subHeader);

                // Tabela z danymi
                var table = new System.Windows.Documents.Table();
                table.CellSpacing = 0;
                table.BorderBrush = Brushes.Black;
                table.BorderThickness = new Thickness(1);

                // Kolumny - uproszczone bez nadgodzin i nocnych
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(180) }); // Pracownik
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(120) }); // Dział
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(45) });  // Dni
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(70) });  // Godziny
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(50) });  // Braki
                table.Columns.Add(new System.Windows.Documents.TableColumn { Width = new GridLength(280) }); // Uwagi - szeroka

                var rowGroup = new System.Windows.Documents.TableRowGroup();
                table.RowGroups.Add(rowGroup);

                // Nagłówek tabeli
                var headerRow = new System.Windows.Documents.TableRow { Background = Brushes.LightGray };
                headerRow.Cells.Add(CreateTableCell("PRACOWNIK", true));
                headerRow.Cells.Add(CreateTableCell("DZIAŁ", true));
                headerRow.Cells.Add(CreateTableCell("DNI", true));
                headerRow.Cells.Add(CreateTableCell("GODZINY", true));
                headerRow.Cells.Add(CreateTableCell("BRAKI", true));
                headerRow.Cells.Add(CreateTableCell("UWAGI", true));
                rowGroup.Rows.Add(headerRow);

                // Dane
                double sumaGodzin = 0;
                int sumaDni = 0, sumaBrakow = 0;
                string aktualnyDzial = "";

                foreach (dynamic item in gridRaportMiesieczny.ItemsSource)
                {
                    // Separator działów
                    if (aktualnyDzial != item.Grupa)
                    {
                        if (!string.IsNullOrEmpty(aktualnyDzial))
                        {
                            var separatorRow = new System.Windows.Documents.TableRow { Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)) };
                            separatorRow.Cells.Add(CreateTableCell("", false, 6));
                            rowGroup.Rows.Add(separatorRow);
                        }
                        aktualnyDzial = item.Grupa;
                    }

                    var row = new System.Windows.Documents.TableRow();
                    row.Cells.Add(CreateTableCell(item.Pracownik));
                    row.Cells.Add(CreateTableCell(item.Grupa));
                    row.Cells.Add(CreateTableCell(item.DniPracy.ToString()));
                    row.Cells.Add(CreateTableCell($"{item.SumaGodzin:N1}", false, 1, true));
                    
                    var brakiCell = CreateTableCell(item.BrakiOdbic.ToString());
                    if (item.BrakiOdbic > 0) brakiCell.Background = new SolidColorBrush(Color.FromRgb(254, 215, 215));
                    row.Cells.Add(brakiCell);
                    
                    row.Cells.Add(CreateTableCell(item.Uwagi ?? ""));
                    rowGroup.Rows.Add(row);

                    sumaGodzin += (double)item.SumaGodzin;
                    sumaDni += (int)item.DniPracy;
                    sumaBrakow += (int)item.BrakiOdbic;
                }

                // Podsumowanie
                var sumRow = new System.Windows.Documents.TableRow { Background = new SolidColorBrush(Color.FromRgb(56, 161, 105)) };
                sumRow.Cells.Add(CreateTableCell("RAZEM:", true, 1, false, Brushes.White));
                sumRow.Cells.Add(CreateTableCell("", false, 1, false, Brushes.White));
                sumRow.Cells.Add(CreateTableCell(sumaDni.ToString(), true, 1, false, Brushes.White));
                sumRow.Cells.Add(CreateTableCell($"{sumaGodzin:N1}", true, 1, false, Brushes.White));
                sumRow.Cells.Add(CreateTableCell(sumaBrakow.ToString(), true, 1, false, Brushes.White));
                sumRow.Cells.Add(CreateTableCell("", false, 1, false, Brushes.White));
                rowGroup.Rows.Add(sumRow);

                document.Blocks.Add(table);

                // Stopka
                var footer = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run($"\nLiczba pracowników: {((System.Collections.ICollection)gridRaportMiesieczny.ItemsSource).Count} • Suma godzin: {sumaGodzin:N1}h • Braki odbić: {sumaBrakow}"))
                {
                    FontSize = 9,
                    Foreground = Brushes.Gray,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 15, 0, 0)
                };
                document.Blocks.Add(footer);

                // Drukowanie
                var paginator = ((System.Windows.Documents.IDocumentPaginatorSource)document).DocumentPaginator;
                printDialog.PrintDocument(paginator, $"Raport {miesiacNazwa} {rok}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd drukowania: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private System.Windows.Documents.TableCell CreateTableCell(string text, bool bold = false, int colSpan = 1, bool highlight = false, Brush foreground = null)
        {
            var cell = new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(text))
            {
                FontSize = 9,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = foreground ?? Brushes.Black,
                Margin = new Thickness(0)
            });
            cell.Padding = new Thickness(4, 2, 4, 2);
            cell.BorderBrush = Brushes.LightGray;
            cell.BorderThickness = new Thickness(0, 0, 1, 1);
            if (colSpan > 1) cell.ColumnSpan = colSpan;
            if (highlight) cell.Background = new SolidColorBrush(Color.FromRgb(237, 242, 247));
            return cell;
        }
        private void BtnOdswiezPorownanie_Click(object sender, RoutedEventArgs e) => UpdateAllViews();
        private void BtnDrukujPorownanie_Click(object sender, RoutedEventArgs e) { }
        private void CmbPracownikEwidencja_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void BtnGenerujKarteEwidencji_Click(object sender, RoutedEventArgs e) { }
        private void BtnDrukujKarteEwidencji_Click(object sender, RoutedEventArgs e) { }

        private void GridRejestracje_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { }
        
        private void GridGodzinyPracy_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridGodzinyPracy.SelectedItem == null) return;
            
            try
            {
                dynamic selected = gridGodzinyPracy.SelectedItem;
                int pracownikId = selected.PracownikId;
                DateTime data = selected.Data;
                string pracownik = selected.Pracownik;
                string grupa = selected.Grupa;
                
                // Znajdź wszystkie rejestracje tego pracownika z tego dnia
                var rejestracje = _wszystkieRejestracje
                    .Where(r => r.PracownikId == pracownikId && r.DataCzas.Date == data.Date)
                    .ToList();
                
                if (rejestracje.Any())
                {
                    var dialog = new SzczegolyDniaWindow(pracownik, grupa, data, rejestracje);
                    dialog.Owner = this;
                    dialog.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Brak szczegółowych danych dla tego dnia.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd otwierania szczegółów: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void GridRanking_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { }
        private void GridRaportMiesieczny_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { }
        private void GridPunktualnosc_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) { }

        // Auto-odświeżanie
        private DispatcherTimer _autoRefreshTimer;
        private void ChkAutoRefresh_Changed(object sender, RoutedEventArgs e)
        {
            if (chkAutoRefresh.IsChecked == true)
            {
                _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
                _autoRefreshTimer.Tick += (s, ev) => LoadAllData();
                _autoRefreshTimer.Start();
            }
            else
            {
                _autoRefreshTimer?.Stop();
            }
        }

        // Agencje
        private void CmbAgencja_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) UpdateAgencje();
        }

        private void BtnGenerujRaportAgencji_Click(object sender, RoutedEventArgs e)
        {
            UpdateAgencje();
        }

        private void UpdateAgencje()
        {
            var agencja = cmbAgencja.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(agencja) || agencja.StartsWith("--")) agencja = null;

            var miesiac = cmbAgencjaMiesiac.SelectedIndex + 1;
            var rok = cmbAgencjaRok.SelectedItem is int r ? r : DateTime.Now.Year;
            var dataOd = new DateTime(rok, miesiac, 1);
            var dataDo = dataOd.AddMonths(1);

            var dane = _wszystkieRejestracje
                .Where(r => r.DataCzas >= dataOd && r.DataCzas < dataDo)
                .Where(r => agencja == null || r.Grupa == agencja)
                .ToList();

            var raport = dane
                .GroupBy(r => r.PracownikId)
                .Select(g =>
                {
                    var pracownik = g.First();
                    double normalne = 0, nadgodziny = 0, nocne = 0;
                    int dniPracy = 0, spoznienia = 0;

                    var byDzien = g.GroupBy(r => r.DataCzas.Date);
                    foreach (var dzien in byDzien)
                    {
                        var we = dzien.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).FirstOrDefault();
                        var wy = dzien.Where(r => r.TypInt == 0).OrderByDescending(r => r.DataCzas).FirstOrDefault();

                        if (we != null && wy != null && wy.DataCzas > we.DataCzas)
                        {
                            dniPracy++;
                            var czas = (wy.DataCzas - we.DataCzas).TotalHours;
                            normalne += Math.Min(8, czas);
                            nadgodziny += Math.Max(0, czas - 8);

                            // Nocne (22:00-06:00)
                            // Uproszczone - można rozbudować

                            if (we.DataCzas.Hour >= 6 && we.DataCzas.Minute > 10)
                                spoznienia++;
                        }
                    }

                    return new
                    {
                        pracownik.Pracownik,
                        DniPracy = dniPracy,
                        GodzinyNormalne = normalne,
                        Nadgodziny = nadgodziny,
                        GodzinyNocne = nocne,
                        SumaGodzin = normalne + nadgodziny + nocne,
                        SredniaDziennie = dniPracy > 0 ? (normalne + nadgodziny) / dniPracy : 0,
                        Spoznienia = spoznienia,
                        Status = spoznienia > 3 ? "⚠️ Dużo spóźnień" : "✅ OK"
                    };
                })
                .OrderByDescending(x => x.SumaGodzin)
                .ToList();

            gridAgencje.ItemsSource = raport;

            // Podsumowanie
            txtAgencjaPracownikow.Text = raport.Count.ToString();
            txtAgencjaDni.Text = raport.Sum(r => r.DniPracy).ToString();
            txtAgencjaGodziny.Text = $"{raport.Sum(r => r.SumaGodzin):N0}h";
            txtAgencjaNadgodziny.Text = $"{raport.Sum(r => r.Nadgodziny):N0}h";
            
            // Oblicz koszt na podstawie stawek z ustawień
            var stawka = UstawieniaStawekWindow.PobierzStawke(agencja ?? "", dataOd);
            var doWyplaty = raport.Sum(r => (decimal)r.GodzinyNormalne * stawka.StawkaPodstawowa + 
                                            (decimal)r.Nadgodziny * stawka.StawkaNadgodzin);
            txtAgencjaDoWyplaty.Text = $"{doWyplaty:N0} zł";
        }

        private void BtnExportAgencja_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx",
                    FileName = $"raport_agencja_{DateTime.Now:yyyyMMdd}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Pracownik;Dni pracy;Normalne;Nadgodziny;Nocne;Suma;Średnia/dzień;Spóźnienia");

                    if (gridAgencje.ItemsSource != null)
                    {
                        foreach (dynamic item in gridAgencje.ItemsSource)
                        {
                            sb.AppendLine($"{item.Pracownik};{item.DniPracy};{item.GodzinyNormalne:N1};{item.Nadgodziny:N1};{item.GodzinyNocne:N1};{item.SumaGodzin:N1};{item.SredniaDziennie:N1};{item.Spoznienia}");
                        }
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Raport wyeksportowany!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GridAgencje_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Otwórz szczegóły pracownika
        }

        // Historia pracownika
        private void CmbHistoriaPracownik_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && cmbHistoriaPracownik.SelectedIndex > 0)
                UpdateHistoriaPracownika();
        }

        private void CmbHistoriaOkres_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded && cmbHistoriaPracownik.SelectedIndex > 0)
                UpdateHistoriaPracownika();
        }

        private void BtnPokazHistorie_Click(object sender, RoutedEventArgs e)
        {
            UpdateHistoriaPracownika();
        }

        private void UpdateHistoriaPracownika()
        {
            if (cmbHistoriaPracownik.SelectedValue is not int pracownikId || pracownikId == 0) return;

            var pracownik = _pracownicy.FirstOrDefault(p => p.Id == pracownikId);
            if (pracownik == null) return;

            // Określ zakres dat
            DateTime dataOd, dataDo = DateTime.Today.AddDays(1);
            switch (cmbHistoriaOkres.SelectedIndex)
            {
                case 0: dataOd = DateTime.Today.AddDays(-7); break;
                case 1: dataOd = DateTime.Today.AddDays(-30); break;
                case 2: dataOd = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); break;
                case 3: 
                    dataOd = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
                    dataDo = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    break;
                case 4: dataOd = DateTime.Today.AddMonths(-3); break;
                case 5: dataOd = new DateTime(DateTime.Today.Year, 1, 1); break;
                default: dataOd = DateTime.Today.AddDays(-30); break;
            }

            // Info o pracowniku
            txtHistoriaNazwisko.Text = pracownik.PelneNazwisko;
            txtHistoriaDzial.Text = pracownik.GrupaNazwa;
            txtHistoriaAgencja.Text = pracownik.GrupaNazwa?.ToUpper().Contains("AGENCJA") == true ? pracownik.GrupaNazwa : "";

            // Pobierz dane z bazy dla tego pracownika (rozszerz zakres)
            var dane = _wszystkieRejestracje
                .Where(r => r.PracownikId == pracownikId && r.DataCzas >= dataOd && r.DataCzas < dataDo)
                .ToList();

            double sumaGodzin = 0, sumaNadgodzin = 0;
            int dniPracy = 0, spoznienia = 0;

            var historia = dane
                .GroupBy(r => r.DataCzas.Date)
                .Select(g =>
                {
                    var we = g.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).FirstOrDefault();
                    var wy = g.Where(r => r.TypInt == 0).OrderByDescending(r => r.DataCzas).FirstOrDefault();

                    double czas = 0, przerwy = 0, nadg = 0;
                    string uwagi = "";

                    if (we != null && wy != null && wy.DataCzas > we.DataCzas)
                    {
                        czas = (wy.DataCzas - we.DataCzas).TotalHours;
                        nadg = Math.Max(0, czas - 8);
                        sumaGodzin += czas;
                        sumaNadgodzin += nadg;
                        dniPracy++;

                        if (we.DataCzas.Hour >= 6 && we.DataCzas.Minute > 10)
                        {
                            spoznienia++;
                            uwagi = "⏰ Spóźnienie";
                        }
                    }
                    else if (we != null && wy == null)
                    {
                        uwagi = "⚠️ Brak wyjścia";
                    }

                    return new
                    {
                        Data = g.Key,
                        DzienTygodnia = g.Key.ToString("ddd"),
                        Wejscie = we?.DataCzas.ToString("HH:mm") ?? "-",
                        Wyjscie = wy?.DataCzas.ToString("HH:mm") ?? "-",
                        CzasPracy = czas > 0 ? $"{(int)czas}:{(int)((czas % 1) * 60):D2}" : "-",
                        Przerwy = "-",
                        Efektywny = czas > 0 ? $"{(int)czas}:{(int)((czas % 1) * 60):D2}" : "-",
                        Nadgodziny = nadg > 0 ? $"+{nadg:N1}h" : "-",
                        Uwagi = uwagi
                    };
                })
                .OrderByDescending(x => x.Data)
                .ToList();

            gridHistoria.ItemsSource = historia;

            // Statystyki
            txtHistoriaGodziny.Text = $"{sumaGodzin:N0}h";
            txtHistoriaNadgodziny.Text = $"{sumaNadgodzin:N0}h";
            txtHistoriaDni.Text = dniPracy.ToString();
            txtHistoriaSrednia.Text = dniPracy > 0 ? $"{sumaGodzin / dniPracy:N1}h" : "0h";
            txtHistoriaSpoznienia.Text = spoznienia.ToString();
        }

        private void GridHistoria_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridHistoria.SelectedItem == null || cmbHistoriaPracownik.SelectedValue is not int pracownikId) return;

            dynamic selected = gridHistoria.SelectedItem;
            DateTime data = selected.Data;
            var pracownik = _pracownicy.FirstOrDefault(p => p.Id == pracownikId);

            var rejestracje = _wszystkieRejestracje
                .Where(r => r.PracownikId == pracownikId && r.DataCzas.Date == data.Date)
                .ToList();

            if (rejestracje.Any() && pracownik != null)
            {
                var dialog = new SzczegolyDniaWindow(pracownik.PelneNazwisko, pracownik.GrupaNazwa, data, rejestracje);
                dialog.Owner = this;
                dialog.ShowDialog();
            }
        }

        // Eksport raportu miesięcznego
        private void BtnExportRaportMiesieczny_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv",
                    FileName = $"raport_miesieczny_{DateTime.Now:yyyyMMdd}.csv"
                };

                if (dialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Pracownik;Dział;Dni pracy;Normalne;Nadgodziny;Nocne;Suma;Urlop;Choroba;Status");

                    if (gridRaportMiesieczny.ItemsSource != null)
                    {
                        foreach (dynamic item in gridRaportMiesieczny.ItemsSource)
                        {
                            sb.AppendLine($"{item.Pracownik};{item.Grupa};{item.DniPracy};{item.GodzinyNormalne:N1};{item.Nadgodziny:N1};{item.GodzinyNocne:N1};{item.SumaGodzin:N1};{item.DniUrlopu};{item.DniChoroby};{item.Status}");
                        }
                    }

                    File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show("Raport wyeksportowany!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd eksportu: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Porównanie miesięcy
        private void BtnPorownajMiesiace_Click(object sender, RoutedEventArgs e)
        {
            // Parsuj wybrane miesiące
            // Format: "Styczeń 2025"
            // Dla uproszczenia używamy indeksów
            try
            {
                var miesiace = new[] { "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec",
                                       "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień" };

                var m1Str = cmbPorownanieMiesiac1.SelectedItem?.ToString();
                var m2Str = cmbPorownanieMiesiac2.SelectedItem?.ToString();

                if (string.IsNullOrEmpty(m1Str) || string.IsNullOrEmpty(m2Str)) return;

                // Parsuj miesiąc i rok
                var parts1 = m1Str.Split(' ');
                var parts2 = m2Str.Split(' ');

                int m1 = Array.IndexOf(miesiace, parts1[0]) + 1;
                int r1 = int.Parse(parts1[1]);
                int m2 = Array.IndexOf(miesiace, parts2[0]) + 1;
                int r2 = int.Parse(parts2[1]);

                var dataOd1 = new DateTime(r1, m1, 1);
                var dataDo1 = dataOd1.AddMonths(1);
                var dataOd2 = new DateTime(r2, m2, 1);
                var dataDo2 = dataOd2.AddMonths(1);

                // Pobierz dane - użyj _wszystkieRejestracje lub załaduj z bazy
                // Dla uproszczenia - używamy tego co mamy załadowane
                // W produkcji trzeba by załadować dane z bazy dla tych miesięcy

                txtPorownGodziny1.Text = "N/A";
                txtPorownGodziny2.Text = "N/A";
                txtPorownGodzinyZmiana.Text = "Załaduj dane";

                MessageBox.Show("Funkcja porównania wymaga załadowania danych z wybranego okresu.\nZmień zakres dat na górze i spróbuj ponownie.", 
                    "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Nowe przyciski - Timeline, Statystyki, Karta RCP, Ustawienia stawek
        private void BtnTimeline_Click(object sender, RoutedEventArgs e)
        {
            var window = new TimelineWindow(_wszystkieRejestracje);
            window.Owner = this;
            window.ShowDialog();
        }

        private void BtnStatystyki_Click(object sender, RoutedEventArgs e)
        {
            var window = new StatystykiWindow(_wszystkieRejestracje);
            window.Owner = this;
            window.ShowDialog();
        }

        private void BtnKartaRCP_Click(object sender, RoutedEventArgs e)
        {
            var window = new KartaRCPWindow(_wszystkieRejestracje, _pracownicy);
            window.Owner = this;
            window.ShowDialog();
        }

        private void BtnZarzadzanieKartami_Click(object sender, RoutedEventArgs e)
        {
            var window = new ZarzadzanieKartamiWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        private void BtnUstawieniaStawek_Click(object sender, RoutedEventArgs e)
        {
            var window = new UstawieniaStawekWindow();
            window.Owner = this;
            if (window.ShowDialog() == true)
            {
                // Odśwież dane po zapisaniu stawek
                UpdateAllViews();
            }
        }

        #endregion

        #region Urlopy i Nieobecności

        private void CmbUrlopyMiesiac_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) LoadUrlopy(); }
        private void CmbUrlopyRok_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) LoadUrlopy(); }

        private void LoadUrlopy()
        {
            if (!IsLoaded || gridUrlopy == null) return;
            // TODO: Załaduj z bazy ZPSP
            gridUrlopy.ItemsSource = new List<NieobecnoscModel>();
        }

        private void BtnDodajNieobecnosc_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DodajNieobecnoscDialog(_pracownicy);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                // TODO: Zapisz do bazy
                LoadUrlopy();
                MessageBox.Show("Nieobecność została dodana.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnZatwierdzNieobecnosc_Click(object sender, RoutedEventArgs e)
        {
            var selected = gridUrlopy.SelectedItem as NieobecnoscModel;
            if (selected == null)
            {
                MessageBox.Show("Wybierz nieobecność do zatwierdzenia.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // TODO: Aktualizuj status w bazie
            LoadUrlopy();
        }

        private void BtnOdrzucNieobecnosc_Click(object sender, RoutedEventArgs e)
        {
            var selected = gridUrlopy.SelectedItem as NieobecnoscModel;
            if (selected == null)
            {
                MessageBox.Show("Wybierz nieobecność do odrzucenia.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // TODO: Aktualizuj status w bazie
            LoadUrlopy();
        }

        #endregion

        #region Nadgodziny

        private void LoadNadgodziny()
        {
            if (!IsLoaded || gridNadgodziny == null) return;
            // TODO: Załaduj z bazy ZPSP
            gridNadgodziny.ItemsSource = new List<NadgodzinyModel>();
        }

        private void BtnDodajNadgodziny_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DodajNadgodzinyDialog(_pracownicy);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                LoadNadgodziny();
                MessageBox.Show("Nadgodziny zostały dodane do kartoteki.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnOdbierzNadgodziny_Click(object sender, RoutedEventArgs e)
        {
            var selected = gridNadgodziny.SelectedItem as NadgodzinyModel;
            if (selected == null)
            {
                MessageBox.Show("Wybierz wpis do odebrania.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // TODO: Dialog odbioru godzin
            LoadNadgodziny();
        }

        private void BtnPrzeliczNadgodziny_Click(object sender, RoutedEventArgs e)
        {
            // Automatyczne wykrywanie nadgodzin z rejestracji
            int wykryte = 0;
            foreach (var pracownik in _pracownicy)
            {
                var rejestracje = _wszystkieRejestracje.Where(r => r.PracownikId == pracownik.Id).ToList();
                // Grupuj po dniach i licz godziny >8
                var poDniach = rejestracje.GroupBy(r => r.DataCzas.Date);
                foreach (var dzien in poDniach)
                {
                    var wejscia = dzien.Where(r => r.TypInt == 1).OrderBy(r => r.DataCzas).ToList();
                    var wyjscia = dzien.Where(r => r.TypInt == 0).OrderBy(r => r.DataCzas).ToList();
                    if (wejscia.Any() && wyjscia.Any())
                    {
                        var pierwszeWejscie = wejscia.First().DataCzas;
                        var ostatnieWyjscie = wyjscia.Last().DataCzas;
                        var godziny = (ostatnieWyjscie - pierwszeWejscie).TotalHours;
                        if (godziny > 8)
                        {
                            wykryte++;
                            // TODO: Zapisz do bazy
                        }
                    }
                }
            }
            MessageBox.Show($"Wykryto {wykryte} dni z nadgodzinami.", "Przeliczanie", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadNadgodziny();
        }

        #endregion

        #region Agencje - Rozliczenia Tygodniowe

        private DateTime _agencjaTydzienStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);

        private void LoadAgencjeTydzien()
        {
            if (!IsLoaded || gridAgencjeTydzien == null || _pracownicy == null) return;

            var lista = new List<AgencjaTydzienModel>();
            var koniecTygodnia = _agencjaTydzienStart.AddDays(6);

            // Pobierz pracowników z agencji
            var agencjePracownicy = _pracownicy.Where(p => 
                p.GrupaNazwa?.ToUpper().Contains("AGENCJA") == true || 
                p.GrupaNazwa?.ToUpper().Contains("GURAVO") == true ||
                p.GrupaNazwa?.ToUpper().Contains("IMPULS") == true).ToList();

            bool maAlerty = false;

            foreach (var pracownik in agencjePracownicy)
            {
                var model = new AgencjaTydzienModel
                {
                    PracownikId = pracownik.Id,
                    Pracownik = pracownik.PelneNazwisko,
                    Agencja = pracownik.GrupaNazwa
                };

                // Oblicz godziny dla każdego dnia
                for (int i = 0; i < 7; i++)
                {
                    var dzien = _agencjaTydzienStart.AddDays(i);
                    var godziny = ObliczGodzinyDnia(pracownik.Id, dzien);
                    
                    switch (i)
                    {
                        case 0: model.Pn = godziny; model.PnKolor = GetKolorGodzin(godziny, true); break;
                        case 1: model.Wt = godziny; model.WtKolor = GetKolorGodzin(godziny, true); break;
                        case 2: model.Sr = godziny; model.SrKolor = GetKolorGodzin(godziny, true); break;
                        case 3: model.Cz = godziny; model.CzKolor = GetKolorGodzin(godziny, true); break;
                        case 4: model.Pt = godziny; model.PtKolor = GetKolorGodzin(godziny, true); break;
                        case 5: model.Sb = godziny; model.SbKolor = GetKolorGodzin(godziny, true); break;
                        case 6: model.Nd = godziny; model.NdKolor = GetKolorGodzin(godziny, true); break;
                    }

                    if (godziny > 12) maAlerty = true;
                }

                model.Suma = model.Pn + model.Wt + model.Sr + model.Cz + model.Pt + model.Sb + model.Nd;
                model.Alert = model.Suma > 60 ? "⚠️" : (new[] { model.Pn, model.Wt, model.Sr, model.Cz, model.Pt, model.Sb, model.Nd }.Any(g => g > 12) ? "🔴" : "");

                if (model.Suma > 0)
                    lista.Add(model);
            }

            gridAgencjeTydzien.ItemsSource = lista;
            if (panelAlertAgencje != null) panelAlertAgencje.Visibility = maAlerty ? Visibility.Visible : Visibility.Collapsed;
            
            if (maAlerty && txtAlertAgencjeOpis != null)
            {
                int przekroczenia = lista.Count(l => new[] { l.Pn, l.Wt, l.Sr, l.Cz, l.Pt, l.Sb, l.Nd }.Any(g => g > 12));
                txtAlertAgencjeOpis.Text = $"Wykryto {przekroczenia} pracowników z przekroczeniem 12h dziennie!";
            }
        }

        private double ObliczGodzinyDnia(int pracownikId, DateTime dzien)
        {
            if (_wszystkieRejestracje == null) return 0;

            var rejestracje = _wszystkieRejestracje
                .Where(r => r.PracownikId == pracownikId && r.DataCzas.Date == dzien.Date)
                .OrderBy(r => r.DataCzas)
                .ToList();

            if (!rejestracje.Any()) return 0;

            var wejscia = rejestracje.Where(r => r.TypInt == 1).ToList();
            var wyjscia = rejestracje.Where(r => r.TypInt == 0).ToList();

            if (wejscia.Any() && wyjscia.Any())
            {
                return (wyjscia.Last().DataCzas - wejscia.First().DataCzas).TotalHours;
            }
            return 0;
        }

        private string GetKolorGodzin(double godziny, bool czyAgencja)
        {
            if (godziny == 0) return "Transparent";
            double limit = czyAgencja ? 12 : 13;
            if (godziny > limit) return "#FED7D7"; // czerwony
            if (godziny > 10) return "#FEEBC8"; // pomarańczowy
            if (godziny > 8) return "#FEFCBF"; // żółty
            return "#C6F6D5"; // zielony
        }

        private void CmbAgencjaTydzien_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) LoadAgencjeTydzien(); }
        private void CmbAgencjaFiltr_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) LoadAgencjeTydzien(); }

        private void BtnAgencjaPoprzedniTydzien_Click(object sender, RoutedEventArgs e)
        {
            _agencjaTydzienStart = _agencjaTydzienStart.AddDays(-7);
            LoadAgencjeTydzien();
        }

        private void BtnAgencjaNastepnyTydzien_Click(object sender, RoutedEventArgs e)
        {
            _agencjaTydzienStart = _agencjaTydzienStart.AddDays(7);
            LoadAgencjeTydzien();
        }

        private void BtnAgencjaBiezacyTydzien_Click(object sender, RoutedEventArgs e)
        {
            _agencjaTydzienStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek + 1);
            LoadAgencjeTydzien();
        }

        private void BtnPokazPrzekroczenia_Click(object sender, RoutedEventArgs e)
        {
            var przekroczenia = (gridAgencjeTydzien.ItemsSource as List<AgencjaTydzienModel>)?
                .Where(l => new[] { l.Pn, l.Wt, l.Sr, l.Cz, l.Pt, l.Sb, l.Nd }.Any(g => g > 12))
                .ToList();

            if (przekroczenia?.Any() == true)
            {
                var msg = string.Join("\n", przekroczenia.Select(p => $"• {p.Pracownik} ({p.Agencja})"));
                MessageBox.Show($"Pracownicy z przekroczeniem 12h:\n\n{msg}", "Przekroczenia", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnExportAgencjeTydzien_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Export do Excel - funkcja w przygotowaniu", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region Spóźnienia

        private void CmbSpoznieniaMiesiac_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) LoadSpoznienia(); }

        private void LoadSpoznienia()
        {
            if (!IsLoaded || gridSpoznienia == null) return;
            // TODO: Załaduj z bazy lub wylicz z rejestracji
            gridSpoznienia.ItemsSource = new List<SpoznienieModel>();
        }

        private void BtnWykryjSpoznienia_Click(object sender, RoutedEventArgs e)
        {
            int tolerancja = (cmbTolerancjaSpoznienia.SelectedIndex) * 5;
            var godzinaStart = new TimeSpan(6, 0, 0); // Domyślna godzina startu
            var spoznienia = new List<SpoznienieModel>();

            var pracownicyZRejestracja = _wszystkieRejestracje
                .Where(r => r.TypInt == 1)
                .GroupBy(r => new { r.PracownikId, r.DataCzas.Date });

            foreach (var grupa in pracownicyZRejestracja)
            {
                var pierwszeWejscie = grupa.OrderBy(r => r.DataCzas).First();
                var godzinaWejscia = pierwszeWejscie.DataCzas.TimeOfDay;
                var spoznienieMin = (int)(godzinaWejscia - godzinaStart).TotalMinutes;

                if (spoznienieMin > tolerancja)
                {
                    spoznienia.Add(new SpoznienieModel
                    {
                        PracownikId = pierwszeWejscie.PracownikId,
                        PracownikNazwa = pierwszeWejscie.Pracownik,
                        GrupaNazwa = pierwszeWejscie.Grupa,
                        Data = pierwszeWejscie.DataCzas.Date,
                        DzienTygodnia = pierwszeWejscie.DataCzas.ToString("ddd"),
                        PlanowanaGodzina = DateTime.Today.Add(godzinaStart),
                        RzeijczystaGodzina = DateTime.Today.Add(godzinaWejscia),
                        SpoznienieMin = spoznienieMin,
                        SpoznienieKolor = spoznienieMin > 30 ? "#E53E3E" : (spoznienieMin > 15 ? "#DD6B20" : "#ECC94B"),
                        Usprawiedliwione = false,
                        StatusIkona = "❌"
                    });
                }
            }

            gridSpoznienia.ItemsSource = spoznienia;
            txtSpoznieniaIlosc.Text = spoznienia.Count.ToString();
            txtSpoznieniaMinuty.Text = spoznienia.Sum(s => s.SpoznienieMin).ToString();
            txtSpoznieniaSrednia.Text = spoznienia.Any() ? $"{spoznienia.Average(s => s.SpoznienieMin):N0} min" : "0 min";
            txtSpoznieniaRecydywisci.Text = spoznienia.GroupBy(s => s.PracownikId).Count(g => g.Count() > 3).ToString();

            MessageBox.Show($"Wykryto {spoznienia.Count} spóźnień.", "Analiza", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnUsprawiedliwSpoznienie_Click(object sender, RoutedEventArgs e)
        {
            var selected = gridSpoznienia.SelectedItem as SpoznienieModel;
            if (selected == null)
            {
                MessageBox.Show("Wybierz spóźnienie do usprawiedliwienia.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            selected.Usprawiedliwione = true;
            selected.StatusIkona = "✅";
            gridSpoznienia.Items.Refresh();
        }

        #endregion

        #region Przerwy

        private void LoadHarmonogramPrzerw()
        {
            if (listHarmonogramPrzerw == null) return;
            
            // Domyślne przerwy
            listHarmonogramPrzerw.ItemsSource = new List<HarmonogramPrzerwyModel>
            {
                new HarmonogramPrzerwyModel { Id = 1, Nazwa = "Przerwa śniadaniowa", GodzinaOd = new TimeSpan(9, 0, 0), GodzinaDo = new TimeSpan(9, 15, 0), CzasTrwaniaMin = 15 },
                new HarmonogramPrzerwyModel { Id = 2, Nazwa = "Przerwa obiadowa", GodzinaOd = new TimeSpan(12, 0, 0), GodzinaDo = new TimeSpan(12, 30, 0), CzasTrwaniaMin = 30 },
                new HarmonogramPrzerwyModel { Id = 3, Nazwa = "Przerwa popołudniowa", GodzinaOd = new TimeSpan(15, 0, 0), GodzinaDo = new TimeSpan(15, 15, 0), CzasTrwaniaMin = 15 }
            };
        }

        private void BtnDodajHarmonogramPrzerwy_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DodajPrzerweDialog();
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                LoadHarmonogramPrzerw();
            }
        }

        private void BtnEdytujPrzerwe_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var id = (int)btn.Tag;
            MessageBox.Show($"Edycja przerwy ID: {id}", "Edycja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAnalizujPrzerwy_Click(object sender, RoutedEventArgs e)
        {
            var data = dpPrzerwyData.SelectedDate ?? DateTime.Today;
            var przerwy = new List<RejestracjaPrzerwyModel>();

            // Znajdź pary wyjście-wejście tego samego dnia (potencjalne przerwy)
            var poDniach = _wszystkieRejestracje
                .Where(r => r.DataCzas.Date == data.Date)
                .GroupBy(r => r.PracownikId);

            foreach (var pracownik in poDniach)
            {
                var rejestracje = pracownik.OrderBy(r => r.DataCzas).ToList();
                for (int i = 0; i < rejestracje.Count - 1; i++)
                {
                    if (rejestracje[i].TypInt == 0 && rejestracje[i + 1].TypInt == 1) // wyjście -> wejście
                    {
                        var wyjscie = rejestracje[i].DataCzas;
                        var wejscie = rejestracje[i + 1].DataCzas;
                        var czas = (int)(wejscie - wyjscie).TotalMinutes;

                        if (czas > 5 && czas < 120) // Przerwa między 5 a 120 minut
                        {
                            // Sprawdź czy w harmonogramie
                            var wHarmonogramie = SprawdzCzyWHarmonogramie(wyjscie.TimeOfDay, wejscie.TimeOfDay);

                            przerwy.Add(new RejestracjaPrzerwyModel
                            {
                                PracownikId = rejestracje[i].PracownikId,
                                PracownikNazwa = rejestracje[i].Pracownik,
                                GodzinaWyjscia = wyjscie.TimeOfDay,
                                GodzinaWejscia = wejscie.TimeOfDay,
                                CzasTrwaniaMin = czas,
                                CzyWHarmonogramie = wHarmonogramie,
                                StatusKolor = wHarmonogramie ? "#38A169" : "#E53E3E",
                                StatusTekst = wHarmonogramie ? "W harmonogramie" : "Poza harmonogramem"
                            });
                        }
                    }
                }
            }

            if (chkTylkoPozaHarmonogramem.IsChecked == true)
                przerwy = przerwy.Where(p => !p.CzyWHarmonogramie).ToList();

            gridPrzerwy.ItemsSource = przerwy;
            txtPrzerwyWHarmonogramie.Text = przerwy.Count(p => p.CzyWHarmonogramie).ToString();
            txtPrzerwyPozaHarmonogramem.Text = przerwy.Count(p => !p.CzyWHarmonogramie).ToString();
            txtPrzerwySredniCzas.Text = przerwy.Any() ? $"{przerwy.Average(p => p.CzasTrwaniaMin):N0} min" : "0 min";
            txtPrzerwyNajdluzsza.Text = przerwy.Any() ? $"{przerwy.Max(p => p.CzasTrwaniaMin)} min" : "0 min";
        }

        private bool SprawdzCzyWHarmonogramie(TimeSpan wyjscie, TimeSpan wejscie)
        {
            var harmonogram = listHarmonogramPrzerw.ItemsSource as List<HarmonogramPrzerwyModel>;
            if (harmonogram == null) return false;

            foreach (var przerwa in harmonogram)
            {
                // Sprawdź czy wyjście i wejście mieszczą się w oknie przerwy (z tolerancją 5 min)
                if (wyjscie >= przerwa.GodzinaOd.Add(TimeSpan.FromMinutes(-5)) &&
                    wejscie <= przerwa.GodzinaDo.Add(TimeSpan.FromMinutes(5)))
                    return true;
            }
            return false;
        }

        #endregion

        #region Alerty

        private void LoadAlerty()
        {
            // Sprawdź czy okno jest załadowane
            if (!IsLoaded || gridAlerty == null) return;

            // Skanuj alerty automatycznie
            var alerty = new List<AlertModel>();

            // Sprawdź przekroczenia godzin
            var poDniach = _wszystkieRejestracje.GroupBy(r => new { r.PracownikId, r.DataCzas.Date });
            foreach (var dzien in poDniach)
            {
                var godziny = ObliczGodzinyDnia(dzien.Key.PracownikId, dzien.Key.Date);
                var pracownik = dzien.First();
                bool czyAgencja = pracownik.Grupa?.ToUpper().Contains("AGENCJA") == true || 
                                  pracownik.Grupa?.ToUpper().Contains("GURAVO") == true ||
                                  pracownik.Grupa?.ToUpper().Contains("IMPULS") == true;

                if (czyAgencja && godziny > 12)
                {
                    alerty.Add(new AlertModel
                    {
                        TypAlertu = "PRZEKROCZENIE_12H",
                        PracownikId = pracownik.PracownikId,
                        PracownikNazwa = pracownik.Pracownik,
                        GrupaNazwa = pracownik.Grupa,
                        CzyAgencja = true,
                        Data = dzien.Key.Date,
                        Wartosc = (decimal)godziny,
                        WartoscTekst = $"{godziny:N1}h",
                        Opis = $"Agencja - przekroczenie limitu 12h",
                        Ikona = "🔴",
                        StatusIkona = "⚪"
                    });
                }
                else if (!czyAgencja && godziny > 13)
                {
                    alerty.Add(new AlertModel
                    {
                        TypAlertu = "PRZEKROCZENIE_13H",
                        PracownikId = pracownik.PracownikId,
                        PracownikNazwa = pracownik.Pracownik,
                        GrupaNazwa = pracownik.Grupa,
                        CzyAgencja = false,
                        Data = dzien.Key.Date,
                        Wartosc = (decimal)godziny,
                        WartoscTekst = $"{godziny:N1}h",
                        Opis = $"Pracownik własny - przekroczenie limitu 13h",
                        Ikona = "🟠",
                        StatusIkona = "⚪"
                    });
                }

                // Sprawdź braki odbić
                var wejscia = dzien.Count(r => r.TypInt == 1);
                var wyjscia = dzien.Count(r => r.TypInt == 0);
                if (wejscia > wyjscia && dzien.Key.Date < DateTime.Today)
                {
                    alerty.Add(new AlertModel
                    {
                        TypAlertu = "BRAK_WYJSCIA",
                        PracownikId = pracownik.PracownikId,
                        PracownikNazwa = pracownik.Pracownik,
                        GrupaNazwa = pracownik.Grupa,
                        Data = dzien.Key.Date,
                        Opis = "Brak rejestracji wyjścia",
                        Ikona = "❌",
                        StatusIkona = "⚪"
                    });
                }
            }

            // Filtrowanie według typu
            string wybranyTyp = "";
            if (cmbAlertyTyp?.SelectedIndex > 0)
            {
                var item = cmbAlertyTyp.SelectedItem as ComboBoxItem;
                wybranyTyp = item?.Content?.ToString() ?? "";
            }

            if (!string.IsNullOrEmpty(wybranyTyp))
            {
                if (wybranyTyp.Contains("12h")) alerty = alerty.Where(a => a.TypAlertu == "PRZEKROCZENIE_12H").ToList();
                else if (wybranyTyp.Contains("13h")) alerty = alerty.Where(a => a.TypAlertu == "PRZEKROCZENIE_13H").ToList();
                else if (wybranyTyp.Contains("wejścia")) alerty = alerty.Where(a => a.TypAlertu == "BRAK_WEJSCIA").ToList();
                else if (wybranyTyp.Contains("wyjścia")) alerty = alerty.Where(a => a.TypAlertu == "BRAK_WYJSCIA").ToList();
                else if (wybranyTyp.Contains("Spóźnienie")) alerty = alerty.Where(a => a.TypAlertu == "SPOZNIENIE").ToList();
            }

            // Filtrowanie według pracownika
            if (cmbAlertyPracownik?.SelectedIndex > 0)
            {
                var pracownik = cmbAlertyPracownik.SelectedItem as PracownikModel;
                if (pracownik != null)
                    alerty = alerty.Where(a => a.PracownikId == pracownik.Id).ToList();
            }

            gridAlerty.ItemsSource = alerty;

            // Aktualizuj liczniki (z null check) - przed filtrowaniem
            var wszystkieAlerty = _wszystkieRejestracje != null ? alerty : new List<AlertModel>();
            if (txtAlertAgencja12 != null) txtAlertAgencja12.Text = alerty.Count(a => a.TypAlertu == "PRZEKROCZENIE_12H").ToString();
            if (txtAlertWlasny13 != null) txtAlertWlasny13.Text = alerty.Count(a => a.TypAlertu == "PRZEKROCZENIE_13H").ToString();
            if (txtAlertBrakWyjscia != null) txtAlertBrakWyjscia.Text = alerty.Count(a => a.TypAlertu == "BRAK_WYJSCIA").ToString();
            if (txtAlertBrakWejscia != null) txtAlertBrakWejscia.Text = alerty.Count(a => a.TypAlertu == "BRAK_WEJSCIA").ToString();
            if (txtAlertSpoznienia != null) txtAlertSpoznienia.Text = alerty.Count(a => a.TypAlertu == "SPOZNIENIE").ToString();
        }

        private void BtnSkanujAlerty_Click(object sender, RoutedEventArgs e)
        {
            LoadAlerty();
        }

        private void BtnOznaczPrzeczytane_Click(object sender, RoutedEventArgs e)
        {
            var selected = gridAlerty.SelectedItems.Cast<AlertModel>().ToList();
            foreach (var alert in selected)
            {
                alert.StatusIkona = "✅";
            }
            gridAlerty.Items.Refresh();
        }

        private void ChkAlertyFiltr_Changed(object sender, RoutedEventArgs e) { if (IsLoaded) LoadAlerty(); }
        private void CmbAlertyTyp_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) LoadAlerty(); }
        private void CmbAlertyPracownik_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) LoadAlerty(); }

        #endregion

        #region Przesunięcia Godzin

        private void LoadPrzesuniecia()
        {
            if (!IsLoaded || gridPrzesuniecia == null) return;
            // TODO: Załaduj z bazy
            gridPrzesuniecia.ItemsSource = new List<PrzesuniecieModel>();
        }

        private void BtnDodajPrzesuniecie_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DodajPrzesuniecieDialog(_pracownicy);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true)
            {
                LoadPrzesuniecia();
                MessageBox.Show("Przesunięcie zostało zapisane.", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnAnulujPrzesuniecie_Click(object sender, RoutedEventArgs e)
        {
            var selected = gridPrzesuniecia.SelectedItem as PrzesuniecieModel;
            if (selected == null)
            {
                MessageBox.Show("Wybierz przesunięcie do anulowania.", "Uwaga", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // TODO: Aktualizuj status w bazie
            LoadPrzesuniecia();
        }

        private void CmbPrzesunieciaStatus_Changed(object sender, SelectionChangedEventArgs e) { if (IsLoaded) LoadPrzesuniecia(); }

        #endregion
    }

    #region Models

    public class GrupaModel
    {
        public int Id { get; set; }
        public string Nazwa { get; set; }
    }

    public class PracownikModel
    {
        public int Id { get; set; }
        public string Imie { get; set; }
        public string Nazwisko { get; set; }
        public int GrupaId { get; set; }
        public string GrupaNazwa { get; set; }
        public string PelneNazwisko => $"{Nazwisko} {Imie}".Trim();
    }

    public class RejestracjaModel
    {
        public DateTime DataCzas { get; set; }
        public string Typ { get; set; }
        public int TypInt { get; set; } // 1=wejście, 0=wyjście
        public int PracownikId { get; set; }
        public string Pracownik { get; set; }
        public int GrupaId { get; set; }
        public string Grupa { get; set; }
        public string PunktDostepu { get; set; }
        public string TypPunktu { get; set; }
        public long NumerKarty { get; set; }
        public string Urzadzenie { get; set; }
        public int TrybRejestracji { get; set; }
    }

    public class NieobecnoscModel
    {
        public int Id { get; set; }
        public int PracownikId { get; set; }
        public string PracownikNazwa { get; set; }
        public int TypNieobecnosciId { get; set; }
        public string TypNazwa { get; set; }
        public DateTime DataOd { get; set; }
        public DateTime DataDo { get; set; }
        public int IloscDni { get; set; }
        public string Status { get; set; }
        public string StatusKolor => Status == "ZATWIERDZONA" ? "#38A169" : (Status == "ODRZUCONA" ? "#E53E3E" : "#DD6B20");
        public string Uwagi { get; set; }
        public DateTime DataUtworzenia { get; set; }
    }

    public class NadgodzinyModel
    {
        public int Id { get; set; }
        public int PracownikId { get; set; }
        public string PracownikNazwa { get; set; }
        public DateTime DataNabicia { get; set; }
        public decimal IloscGodzin { get; set; }
        public decimal IloscOdebrana { get; set; }
        public decimal DoOdebrania => IloscGodzin - IloscOdebrana;
        public DateTime? DataOdbioru { get; set; }
        public string Status { get; set; }
        public string StatusNazwa => Status == "DO_ODBIORU" ? "Do odbioru" : (Status == "CZESCIOWO" ? "Częściowo" : "Odebrane");
        public string StatusKolor => Status == "ODEBRANE" ? "#38A169" : (Status == "CZESCIOWO" ? "#DD6B20" : "#3182CE");
        public string Uwagi { get; set; }
    }

    public class AgencjaTydzienModel
    {
        public int PracownikId { get; set; }
        public string Pracownik { get; set; }
        public string Agencja { get; set; }
        public double Pn { get; set; }
        public double Wt { get; set; }
        public double Sr { get; set; }
        public double Cz { get; set; }
        public double Pt { get; set; }
        public double Sb { get; set; }
        public double Nd { get; set; }
        public double Suma { get; set; }
        public string PnKolor { get; set; }
        public string WtKolor { get; set; }
        public string SrKolor { get; set; }
        public string CzKolor { get; set; }
        public string PtKolor { get; set; }
        public string SbKolor { get; set; }
        public string NdKolor { get; set; }
        public string Alert { get; set; }
    }

    public class SpoznienieModel
    {
        public int Id { get; set; }
        public int PracownikId { get; set; }
        public string PracownikNazwa { get; set; }
        public string GrupaNazwa { get; set; }
        public DateTime Data { get; set; }
        public string DzienTygodnia { get; set; }
        public DateTime PlanowanaGodzina { get; set; }
        public DateTime RzeijczystaGodzina { get; set; }
        public int SpoznienieMin { get; set; }
        public string SpoznienieKolor { get; set; }
        public bool Usprawiedliwione { get; set; }
        public string StatusIkona { get; set; }
        public string Uwagi { get; set; }
    }

    public class HarmonogramPrzerwyModel
    {
        public int Id { get; set; }
        public string Nazwa { get; set; }
        public TimeSpan GodzinaOd { get; set; }
        public TimeSpan GodzinaDo { get; set; }
        public int CzasTrwaniaMin { get; set; }
    }

    public class RejestracjaPrzerwyModel
    {
        public int Id { get; set; }
        public int PracownikId { get; set; }
        public string PracownikNazwa { get; set; }
        public TimeSpan GodzinaWyjscia { get; set; }
        public TimeSpan? GodzinaWejscia { get; set; }
        public int CzasTrwaniaMin { get; set; }
        public bool CzyWHarmonogramie { get; set; }
        public string StatusKolor { get; set; }
        public string StatusTekst { get; set; }
        public string HarmonogramNazwa { get; set; }
    }

    public class AlertModel
    {
        public int Id { get; set; }
        public string TypAlertu { get; set; }
        public int PracownikId { get; set; }
        public string PracownikNazwa { get; set; }
        public string GrupaNazwa { get; set; }
        public bool CzyAgencja { get; set; }
        public DateTime Data { get; set; }
        public string Opis { get; set; }
        public decimal? Wartosc { get; set; }
        public string WartoscTekst { get; set; }
        public string Ikona { get; set; }
        public string StatusIkona { get; set; }
    }

    public class PrzesuniecieModel
    {
        public int Id { get; set; }
        public int PracownikId { get; set; }
        public string PracownikNazwa { get; set; }
        public DateTime DataPrzesunieciaOd { get; set; }
        public DateTime? DataPrzesunieciaDo { get; set; }
        public TimeSpan NowaGodzinaStart { get; set; }
        public string Powod { get; set; }
        public string Status { get; set; }
        public string StatusKolor => Status == "AKTYWNE" ? "#38A169" : (Status == "ZAKONCZONE" ? "#718096" : "#E53E3E");
        public DateTime DataUtworzenia { get; set; }
    }

    #endregion

    #region Dialogs

    public class DodajNieobecnoscDialog : Window
    {
        private ComboBox cmbPracownik, cmbTyp;
        private DatePicker dpOd, dpDo;
        private TextBox txtUwagi;

        public DodajNieobecnoscDialog(List<PracownikModel> pracownicy)
        {
            Title = "Dodaj nieobecność";
            Width = 450;
            Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5F7FA"));

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Pracownik
            var sp1 = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            sp1.Children.Add(new TextBlock { Text = "Pracownik:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            cmbPracownik = new ComboBox { ItemsSource = pracownicy, DisplayMemberPath = "PelneNazwisko" };
            sp1.Children.Add(cmbPracownik);
            Grid.SetRow(sp1, 0);
            grid.Children.Add(sp1);

            // Typ
            var sp2 = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            sp2.Children.Add(new TextBlock { Text = "Typ nieobecności:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            cmbTyp = new ComboBox();
            cmbTyp.Items.Add("Urlop wypoczynkowy");
            cmbTyp.Items.Add("Urlop na żądanie");
            cmbTyp.Items.Add("Zwolnienie chorobowe (L4)");
            cmbTyp.Items.Add("Urlop okolicznościowy");
            cmbTyp.Items.Add("Opieka nad dzieckiem");
            cmbTyp.Items.Add("Urlop bezpłatny");
            cmbTyp.SelectedIndex = 0;
            sp2.Children.Add(cmbTyp);
            Grid.SetRow(sp2, 1);
            grid.Children.Add(sp2);

            // Daty
            var sp3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            sp3.Children.Add(new TextBlock { Text = "Od:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            dpOd = new DatePicker { Width = 120, SelectedDate = DateTime.Today };
            sp3.Children.Add(dpOd);
            sp3.Children.Add(new TextBlock { Text = "Do:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 8, 0) });
            dpDo = new DatePicker { Width = 120, SelectedDate = DateTime.Today };
            sp3.Children.Add(dpDo);
            Grid.SetRow(sp3, 2);
            grid.Children.Add(sp3);

            // Uwagi
            var sp4 = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            sp4.Children.Add(new TextBlock { Text = "Uwagi:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 5) });
            txtUwagi = new TextBox { Height = 60, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
            sp4.Children.Add(txtUwagi);
            Grid.SetRow(sp4, 3);
            grid.Children.Add(sp4);

            // Przyciski
            var spBtn = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnZapisz = new Button { Content = "Zapisz", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(0, 0, 10, 0) };
            btnZapisz.Click += (s, e) => { DialogResult = true; Close(); };
            var btnAnuluj = new Button { Content = "Anuluj", Padding = new Thickness(20, 8, 20, 8) };
            btnAnuluj.Click += (s, e) => { DialogResult = false; Close(); };
            spBtn.Children.Add(btnZapisz);
            spBtn.Children.Add(btnAnuluj);
            Grid.SetRow(spBtn, 5);
            grid.Children.Add(spBtn);

            Content = grid;
        }
    }

    public class DodajNadgodzinyDialog : Window
    {
        public DodajNadgodzinyDialog(List<PracownikModel> pracownicy)
        {
            Title = "Dodaj nadgodziny do odbioru";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var sp = new StackPanel { Margin = new Thickness(20) };
            sp.Children.Add(new TextBlock { Text = "Funkcja w przygotowaniu...", FontSize = 14 });
            
            var btn = new Button { Content = "OK", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            btn.Click += (s, e) => Close();
            sp.Children.Add(btn);

            Content = sp;
        }
    }

    public class DodajPrzerweDialog : Window
    {
        public DodajPrzerweDialog()
        {
            Title = "Dodaj przerwę do harmonogramu";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var sp = new StackPanel { Margin = new Thickness(20) };
            sp.Children.Add(new TextBlock { Text = "Funkcja w przygotowaniu...", FontSize = 14 });
            
            var btn = new Button { Content = "OK", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            btn.Click += (s, e) => Close();
            sp.Children.Add(btn);

            Content = sp;
        }
    }

    public class DodajPrzesuniecieDialog : Window
    {
        public DodajPrzesuniecieDialog(List<PracownikModel> pracownicy)
        {
            Title = "Dodaj przesunięcie godzin";
            Width = 450;
            Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var sp = new StackPanel { Margin = new Thickness(20) };
            sp.Children.Add(new TextBlock { Text = "Funkcja w przygotowaniu...", FontSize = 14 });
            
            var btn = new Button { Content = "OK", Padding = new Thickness(20, 8, 20, 8), Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
            btn.Click += (s, e) => Close();
            sp.Children.Add(btn);

            Content = sp;
        }
    }

    #endregion
}
