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
                                Id = Convert.ToInt32(reader.GetValue(0)),
                                Nazwa = reader.GetValue(1)?.ToString() ?? ""
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
                                Id = Convert.ToInt32(reader.GetValue(0)),
                                Imie = reader.IsDBNull(1) ? "" : reader.GetValue(1)?.ToString() ?? "",
                                Nazwisko = reader.IsDBNull(2) ? "" : reader.GetValue(2)?.ToString() ?? "",
                                GrupaId = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3)),
                                GrupaNazwa = reader.IsDBNull(4) ? "" : reader.GetValue(4)?.ToString() ?? ""
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

                    // Kolumny z V_KDINAR_ALL_REGISTRATIONS - tylko podstawowe, pewne kolumny
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
                                // KDINAR_REGISTRTN_TYPE może być int lub string - obsłuż oba przypadki
                                int typZBazy = 0;
                                if (!reader.IsDBNull(1))
                                {
                                    var rawType = reader.GetValue(1);
                                    if (rawType is int i) typZBazy = i;
                                    else if (rawType is string s)
                                    {
                                        if (s.ToUpper().Contains("WE") || s.ToUpper().Contains("IN") || s.ToUpper().Contains("ENTRY")) typZBazy = 1;
                                        else typZBazy = 0;
                                    }
                                    else typZBazy = Convert.ToInt32(rawType);
                                }

                                // Określ typ wejścia/wyjścia na podstawie nazwy punktu dostępu
                                int typInt = OkreslTypWejsciaWyjscia(punktDostepu, typZBazy);
                                string typ = typInt == 1 ? "WEJŚCIE" : "WYJŚCIE";

                                var reg = new RejestracjaModel
                                {
                                    DataCzas = reader.GetDateTime(0),
                                    Typ = typ,
                                    TypInt = typInt,
                                    PracownikId = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                                    Pracownik = $"{(reader.IsDBNull(4) ? "" : reader.GetValue(4)?.ToString())} {(reader.IsDBNull(3) ? "" : reader.GetValue(3)?.ToString())}".Trim(),
                                    GrupaId = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5)),
                                    PunktDostepu = punktDostepu,
                                    NumerKarty = 0,
                                    Urzadzenie = "",
                                    TrybRejestracji = 0
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
                                int typZBazy = 0;
                                if (!reader.IsDBNull(1))
                                {
                                    var rawType = reader.GetValue(1);
                                    if (rawType is int i) typZBazy = i;
                                    else if (rawType is string s)
                                    {
                                        if (s.ToUpper().Contains("WE") || s.ToUpper().Contains("IN") || s.ToUpper().Contains("ENTRY")) typZBazy = 1;
                                        else typZBazy = 0;
                                    }
                                    else typZBazy = Convert.ToInt32(rawType);
                                }
                                int typInt = OkreslTypWejsciaWyjscia(punktDostepu, typZBazy);

                                var reg = new RejestracjaModel
                                {
                                    DataCzas = reader.GetDateTime(0),
                                    TypInt = typInt,
                                    PracownikId = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2)),
                                    Pracownik = $"{(reader.IsDBNull(4) ? "" : reader.GetValue(4)?.ToString())} {(reader.IsDBNull(3) ? "" : reader.GetValue(3)?.ToString())}".Trim(),
                                    GrupaId = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader.GetValue(5))
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

        #region Tutorial / Pomoc

        private void BtnTutorial_Click(object sender, RoutedEventArgs e)
        {
            var window = new Window
            {
                Title = "Pomoc - Kontrola Czasu Pracy",
                Width = 1000,
                Height = 800,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(250, 251, 252))
            };
            WindowIconHelper.SetIcon(window);

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // === LEWY PANEL - SPIS TRESCI ===
            var navBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),
                Padding = new Thickness(0)
            };

            var navStack = new StackPanel();
            var navScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = navStack };

            // Naglowek nawigacji
            var navHeader = new TextBlock
            {
                Text = "SPIS TRESCI",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(160, 174, 192)),
                Margin = new Thickness(16, 16, 16, 12)
            };
            navStack.Children.Add(navHeader);

            // === PRAWY PANEL - TRESC ===
            var contentScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(32, 24, 32, 24)
            };
            var contentStack = new StackPanel();
            contentScroll.Content = contentStack;

            // Definicje rozdzialow
            var rozdzialy = new List<(string ikona, string tytul, string tresc)>
            {
                ("🏠", "Witaj!", GetTutorialWitaj()),
                ("📊", "Dashboard", GetTutorialDashboard()),
                ("📋", "Rejestracje", GetTutorialRejestracje()),
                ("⏰", "Godziny pracy", GetTutorialGodzinyPracy()),
                ("👥", "Obecni", GetTutorialObecni()),
                ("🏢", "Agencje", GetTutorialAgencje()),
                ("📅", "Raport miesięczny", GetTutorialRaport()),
                ("🏆", "Ranking", GetTutorialRanking()),
                ("⏱️", "Punktualność", GetTutorialPunktualnosc()),
                ("⏱️", "Spóźnienia", GetTutorialSpoznienia()),
                ("☕", "Przerwy", GetTutorialPrzerwy()),
                ("🚨", "Alerty", GetTutorialAlerty()),
                ("🔍", "Filtrowanie", GetTutorialFiltrowanie()),
                ("📥", "Eksport danych", GetTutorialEksport()),
                ("🔧", "Diagnostyka", GetTutorialDiagnostyka()),
                ("⌨️", "Skróty i triki", GetTutorialSkroty()),
            };

            // Dodaj pierwszy rozdzial
            void PokazRozdzial(int index)
            {
                contentStack.Children.Clear();
                var (ikona, tytul, tresc) = rozdzialy[index];

                var header = new TextBlock
                {
                    Text = $"{ikona}  {tytul}",
                    FontSize = 24,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(26, 32, 44)),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                contentStack.Children.Add(header);

                var separator = new Border
                {
                    Height = 3,
                    Background = new SolidColorBrush(Color.FromRgb(66, 153, 225)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Width = 60,
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 0, 0, 20)
                };
                contentStack.Children.Add(separator);

                // Parsuj tresc - linie zaczynajace sie od ## to naglowki, --- to separatory
                foreach (var linia in tresc.Split('\n'))
                {
                    var l = linia.TrimEnd('\r');
                    if (l.StartsWith("## "))
                    {
                        var sub = new TextBlock
                        {
                            Text = l.Substring(3),
                            FontSize = 16,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Color.FromRgb(44, 82, 130)),
                            Margin = new Thickness(0, 18, 0, 6)
                        };
                        contentStack.Children.Add(sub);
                    }
                    else if (l.StartsWith("───") || l.StartsWith("---"))
                    {
                        var sep = new Border
                        {
                            Height = 1,
                            Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                            Margin = new Thickness(0, 8, 0, 8)
                        };
                        contentStack.Children.Add(sep);
                    }
                    else if (l.StartsWith("  TIP:") || l.StartsWith("  WSKAZOWKA:"))
                    {
                        var tip = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(235, 248, 255)),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(66, 153, 225)),
                            BorderThickness = new Thickness(3, 0, 0, 0),
                            Padding = new Thickness(12, 8, 12, 8),
                            Margin = new Thickness(0, 4, 0, 4),
                            CornerRadius = new CornerRadius(0, 4, 4, 0)
                        };
                        tip.Child = new TextBlock
                        {
                            Text = l.Trim(),
                            FontSize = 12,
                            Foreground = new SolidColorBrush(Color.FromRgb(44, 82, 130)),
                            TextWrapping = TextWrapping.Wrap
                        };
                        contentStack.Children.Add(tip);
                    }
                    else if (l.StartsWith("  UWAGA:"))
                    {
                        var warn = new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(255, 251, 235)),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(237, 137, 54)),
                            BorderThickness = new Thickness(3, 0, 0, 0),
                            Padding = new Thickness(12, 8, 12, 8),
                            Margin = new Thickness(0, 4, 0, 4),
                            CornerRadius = new CornerRadius(0, 4, 4, 0)
                        };
                        warn.Child = new TextBlock
                        {
                            Text = l.Trim(),
                            FontSize = 12,
                            Foreground = new SolidColorBrush(Color.FromRgb(146, 64, 14)),
                            TextWrapping = TextWrapping.Wrap
                        };
                        contentStack.Children.Add(warn);
                    }
                    else
                    {
                        var txt = new TextBlock
                        {
                            Text = l,
                            FontSize = 13,
                            Foreground = new SolidColorBrush(Color.FromRgb(45, 55, 72)),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 2, 0, 2),
                            LineHeight = 20
                        };
                        contentStack.Children.Add(txt);
                    }
                }

                contentScroll.ScrollToTop();

                // Podswietl aktywny przycisk
                foreach (var child in navStack.Children)
                {
                    if (child is Button btn)
                        btn.Background = System.Windows.Media.Brushes.Transparent;
                }
                if (navStack.Children.Count > index + 1 && navStack.Children[index + 1] is Button activeBtn)
                    activeBtn.Background = new SolidColorBrush(Color.FromRgb(45, 55, 72));
            }

            // Buduj nawigacje
            for (int i = 0; i < rozdzialy.Count; i++)
            {
                var idx = i;
                var (ikona, tytul, _) = rozdzialy[i];
                var btn = new Button
                {
                    Content = $" {ikona}  {tytul}",
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Padding = new Thickness(16, 8, 16, 8),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btn.Click += (s, ev) => PokazRozdzial(idx);
                navStack.Children.Add(btn);
            }

            navBorder.Child = navScroll;
            Grid.SetColumn(navBorder, 0);
            mainGrid.Children.Add(navBorder);

            Grid.SetColumn(contentScroll, 1);
            mainGrid.Children.Add(contentScroll);

            PokazRozdzial(0);

            window.Content = mainGrid;
            window.ShowDialog();
        }

        private string GetTutorialWitaj() => @"
Witaj w module Kontrola Czasu Pracy!

Ten program pozwala sledzic czas pracy pracownikow na podstawie
danych z czytnikow kart UNICARD RCP (Rejestracja Czasu Pracy).
Kazdy pracownik posiada karte, ktora przykladka do czytnika
przy wejsciu i wyjsciu z zakladu. Program zbiera te dane
i przetwarza je na czytelne raporty.

## Co znajdziesz w programie?

  👁️  Podglad na zywo kto jest w pracy
  ⏰  Godziny pracy kazdego pracownika
  📊  Raporty miesieczne i tygodniowe
  🏢  Rozliczenia agencji pracy tymczasowej
  🚨  Alerty o przekroczeniach i brakach odbic
  🏆  Ranking pracownikow wg godzin i punktualnosci
  ☕  Kontrola przerw i nieobecnosci
  📥  Eksport danych do CSV / Excel / druk

## Jak zaczac? - krok po kroku

  1.  Program laduje dane automatycznie po uruchomieniu
      (poczekaj az pasek stanu na dole pokaze 'Polaczono')
  2.  Na gorze widzisz aktualny czas i liczbe obecnych osob
  3.  Uzyj zakladek u dolu aby przegladac rozne widoki danych
  4.  Filtruj dane po dziale, dacie lub wyszukaj pracownika po imieniu

  TIP: Zacznij od zakladki Dashboard - tam masz przeglad calego dnia!

## Przyklad: typowy dzien pracy z programem

  Rano otwierasz program i patrzysz na Dashboard:
  - Widzisz ze 45 pracownikow jest juz w pracy
  - Zauwazasz 2 alerty - ktos nie odbil karty wczoraj
  - Przechodzisz do zakladki Alerty i sprawdzasz szczegoly
  - Na koniec dnia generujesz raport i sprawdzasz godziny

---

## Uklad ekranu - co jest gdzie?

  ┌─────────────────────────────────────────────────┐
  │  📖 Pomoc  📊 Timeline  📈 Statystyki     14:30 │  ← Gora: przyciski + zegar
  ├─────────────────────────────────────────────────┤
  │  Od: [data]  Do: [data]  Dzial: [▼]  Szukaj    │  ← Filtry
  ├─────────────────────────────────────────────────┤
  │                                                 │
  │  📋 Rejestracje │ ⏰ Godziny │ 🏢 Agencje │... │  ← Zakladki
  │                                                 │
  │       (tutaj sa dane - tabele, wykresy)         │
  │                                                 │
  ├─────────────────────────────────────────────────┤
  │  UNICARD v2.0 • 192.168.0.23 • 🟢 Polaczono   │  ← Stopka
  └─────────────────────────────────────────────────┘

  GORNY PASEK:  Przyciski funkcyjne (Pomoc, Timeline, Statystyki,
                Karty, Druk), zegar z data, licznik obecnych
  FILTRY:       Zakres dat (Od/Do), wybor dzialu, pole szukania
  ZAKLADKI:     Kazda zakladka to inny widok danych (kliknij nazwe)
  STOPKA:       Status polaczenia z serwerem, wersja, diagnostyka

  TIP: Zielona kropka w stopce oznacza ze polaczenie dziala.
  Czerwona = problem z serwerem (kliknij 🔧 aby zdiagnozowac).

  UWAGA: Jesli widzisz 'Brak polaczenia' w stopce, dane moga
  byc nieaktualne! Sprawdz polaczenie sieciowe.
";

        private string GetTutorialDashboard() => @"
Dashboard to glowny ekran programu. Pokazuje podsumowanie
calego dnia w jednym miejscu - nie musisz klikac w inne zakladki.

## Kafelki na gorze - szybki przeglad

  👥 OBECNI       Ilu pracownikow jest TERAZ w zakladzie
  ➡️ WEJSCIA      Ile osob weszlo dzisiaj (unikalne osoby)
  ⬅️ WYJSCIA      Ile osob wyszlo dzisiaj
  ⏱️ GODZINY      Suma godzin wszystkich pracownikow lacznie
  🚨 ALERTY       Ile jest aktywnych alertow (problemow do sprawdzenia)

## Przyklad: co mowia kafelki?

  Jesli widzisz:  OBECNI: 38 / WEJSCIA: 52 / WYJSCIA: 14
  To znaczy, ze:
  - 52 osoby przylozily karte na wejsciu dzisiaj
  - 14 osob juz wyszlo z pracy
  - 38 osob jest nadal na terenie zakladu (52 - 14 = 38)

---

## Obecnosc wg lokalizacji

  Kolorowe kafelki pokazuja ilu pracownikow jest na poszczegolnych
  dzialach. Kazdy dzial ma swoj kolor:

  🟦 Produkcja       np. 18 osob
  🟩 Strefa czysta   np. 8 osob
  🟨 Strefa brudna   np. 5 osob
  🟪 Myjka           np. 3 osoby
  🟧 Mechanicy       np. 4 osoby
  ⬜ Biuro            np. 6 osob

  TIP: Duzy kafelek = duzo pracownikow. Na pierwszy rzut oka
  widzisz gdzie jest najwiecej ludzi!

---

## Ostatnie wejscia / wyjscia

  Dwie tabelki na dole pokazuja 10 ostatnich rejestacji:

  OSTATNIE WEJSCIA:               OSTATNIE WYJSCIA:
  ┌──────────┬───────┬─────────┐  ┌──────────┬───────┬──────────┐
  │ 06:02    │ WE    │ Kowalski│  │ 14:05    │ WY    │ Nowak    │
  │ 06:05    │ WE    │ Nowak   │  │ 14:08    │ WY    │ Zielinski│
  │ 06:07    │ WE    │ Wisnia  │  │ 14:10    │ WY    │ Krawczyk │
  └──────────┴───────┴─────────┘  └──────────┴───────┴──────────┘

  Dzieki temu widzisz na zywo kto wlasnie przyszedl lub wyszedl.

---

## Automatyczne odswiezanie

  TIP: Dashboard odswieza sie co 30 sekund jesli wlaczysz
  checkbox 'Auto' w pasku filtrow! Mozesz zostawic program
  otwarty na monitorze i patrzec na aktualne dane caly dzien.

  UWAGA: Bez wlaczonego 'Auto' dane pokaza stan z momentu
  ostatniego odswiezenia. Kliknij 'Odswiez' reczne aby
  zaktualizowac.
";

        private string GetTutorialRejestracje() => @"
Zakladka Rejestracje pokazuje surowe dane z czytnikow kart.
To sa oryginalne zapisy - kazde przylozenie karty do czytnika
tworzy jeden wiersz w tej tabeli.

## Co widzisz w tabeli? - opis kolumn

  DATA          Dzien rejestracji (np. 2026-02-13)
  GODZINA       Dokladna godzina przylozenia karty (np. 06:02:15)
  TYP           WEJSCIE lub WYJSCIE
  PRACOWNIK     Imie i nazwisko osoby
  DZIAL         Do jakiego dzialu nalezy pracownik (np. Produkcja)
  PUNKT         Nazwa czytnika (np. Portiernia WE, Produkcja WY)

## Przyklad: jak wyglada typowy dzien pracownika?

  Pracownik Jan Kowalski z dzialu Produkcja:

  ┌────────────┬──────────┬─────────┬──────────────┬────────────┐
  │ 2026-02-13 │ 05:58:22 │ WEJSCIE │ Jan Kowalski │ Produkcja  │
  │ 2026-02-13 │ 09:01:45 │ WYJSCIE │ Jan Kowalski │ Produkcja  │
  │ 2026-02-13 │ 09:16:30 │ WEJSCIE │ Jan Kowalski │ Produkcja  │
  │ 2026-02-13 │ 14:02:10 │ WYJSCIE │ Jan Kowalski │ Produkcja  │
  └────────────┴──────────┴─────────┴──────────────┴────────────┘

  Co to znaczy?
  - 05:58 - Przyszedl do pracy (przylozyl karte na wejsciu)
  - 09:01 - Wyszedl na przerwe sniadaniowa
  - 09:16 - Wrocil z przerwy (15 minut przerwy)
  - 14:02 - Zakonczyl prace i wyszedl

---

## Jak czytac dane?

  Kazdy wiersz = jedno przylozenie karty do czytnika.
  Pracownik moze miec wiele wpisow dziennie:

  Typowy schemat:
  1. WEJSCIE rano (poczatek pracy)
  2. WYJSCIE na przerwe sniadaniowa
  3. WEJSCIE po przerwie
  4. WYJSCIE na obiad (opcjonalnie)
  5. WEJSCIE po obiedzie (opcjonalnie)
  6. WYJSCIE koniec pracy

  TIP: Jesli pracownik ma tylko WEJSCIE bez WYJSCIA to albo
  nadal jest w pracy, albo zapomnial odbic karte!

---

## Filtrowanie rejestacji

  Uzyj pola 'Szukaj' na gorze aby znalezc konkretnego pracownika.
  Wpisz imie, nazwisko lub nazwe dzialu.

  Przyklady wyszukiwania:
  - Wpisz 'Kowalski' → zobaczysz wszystkie odbicia Kowalskiego
  - Wpisz 'Produkcja' → tylko pracownicy z dzialu Produkcja
  - Wpisz 'Jan' → wszyscy o imieniu Jan

  TIP: Kliknij dwukrotnie na wiersz aby zobaczyc szczegoly
  calego dnia danego pracownika!

  UWAGA: Duza ilosc danych (np. caly miesiac) moze dluzej sie
  ladowac. Zacznij od krotszych okresow (dzis, wczoraj, tydzien).
";

        private string GetTutorialGodzinyPracy() => @"
Ta zakladka przelicza surowe rejestracje na godziny pracy.
Zamiast surowych odbic karty widzisz gotowe podsumowanie:
ile kto pracowal, ile mial przerw, ile nadgodzin.

## Kolumny w tabeli - co znaczy kazda?

  DATA          Dzien pracy (np. 2026-02-13)
  DZIEN         Dzien tygodnia (Pon, Wt, Sr, Czw, Pt, Sob, Nd)
  PRACOWNIK     Imie i nazwisko pracownika
  DZIAL         Nazwa dzialu (np. Produkcja, Biuro)
  WEJSCIE       Godzina PIERWSZEGO wejscia tego dnia
  WYJSCIE       Godzina OSTATNIEGO wyjscia tego dnia
  CZAS PRACY    Calkowity czas (wyjscie minus wejscie)
  PRZERWY       Ile czasu lacznie spedzono na przerwach
  EFEKTYWNY     Czas pracy minus przerwy (faktycznie przepracowane)
  NADGODZINY    Godziny ponad 8h dziennie (efektywne)
  STATUS        Ikona statusu pracownika

## Przyklad: jak czytac wiersz tabeli?

  ┌──────────┬─────┬──────────────┬───────┬───────┬──────┬────────┬─────┬───────┬────────┬────┐
  │ DATA     │DZIEN│ PRACOWNIK    │ DZIAL │WEJSCIE│WYJSC.│CZ.PRACY│PRZE.│EFEKT. │NADGODZ.│ ST │
  ├──────────┼─────┼──────────────┼───────┼───────┼──────┼────────┼─────┼───────┼────────┼────┤
  │2026-02-13│ Czw │ Jan Kowalski │ Prod. │ 05:58 │14:02 │ 8:04   │0:15 │ 7:49  │  0:00  │ ✅ │
  │2026-02-13│ Czw │ Anna Nowak   │ Biuro │ 07:02 │16:35 │ 9:33   │0:30 │ 9:03  │  1:03  │ ✅ │
  │2026-02-13│ Czw │ Piotr Ziel.  │ Prod. │ 06:00 │      │        │     │       │        │ ⚠️ │
  └──────────┴─────┴──────────────┴───────┴───────┴──────┴────────┴─────┴───────┴────────┴────┘

  Co tu widzimy?
  - Kowalski: przyszedl 5:58, wyszedl 14:02, pracowal 7h49min (z przerwa 15min). OK
  - Nowak: przyszla 7:02, wyszla 16:35, pracowal 9h03min efektywnie. 1h03min nadgodzin
  - Zielinski: przyszedl 6:00 ale NIE MA WYJSCIA - moze jest jeszcze w pracy albo
    zapomnial odbic karte → status ⚠️

---

## Statusy - co oznaczaja ikony?

  ✅  OK - normalny dzien pracy (do 10h, ma wejscie i wyjscie)
  ⚠️  Brak wyjscia - pracownik nie odbil karty na wyjsciu
  🔴  Dlugi dzien - ponad 12 godzin pracy (wymaga uwagi!)

  TIP: Status ⚠️ (brak wyjscia) jest najczestszym problemem.
  Zwykle oznacza ze pracownik zapomnial przylozyc karte
  na wyjsciu. Sprawdz to z przelozonym dzialu.

---

## Podsumowanie na gorze

  Pod tabela sa 4 kafelki ze statystykami za wybrany okres:

  SREDNI EFEKTYWNY   np. 7h 42min  (srednia na pracownika dziennie)
  SREDNI CALKOWITY   np. 8h 15min  (srednia lacznie z przerwami)
  LACZNE GODZINY     np. 1,248h    (suma godzin wszystkich)
  LACZNE NADGODZINY  np. 87h       (suma nadgodzin wszystkich)

---

## Jak uzywac?

  1.  Wybierz zakres dat (np. Od: 01.02 Do: 28.02)
  2.  Opcjonalnie wybierz dzial (np. tylko Produkcja)
  3.  Dane laduja sie automatycznie
  4.  Sortuj klikajac na naglowek kolumny (np. kliknij NADGODZINY)
  5.  Kliknij dwukrotnie na wiersz → szczegoly dnia

  TIP: Kliknij na naglowek kolumny NADGODZINY aby posortowac
  od najwiekszej. Szybko znajdziesz kto ma najwiecej nadgodzin!

  UWAGA: Jesli widzisz '0:00' w kolumnie CZAS PRACY ale pracownik
  mial wejscie, to prawdopodobnie nie odbil karty na wyjsciu.
";

        private string GetTutorialObecni() => @"
Zakladka Obecni pokazuje kto TERAZ jest w zakladzie.
To jest 'na zywo' - widzisz kto aktualnie przebywa na terenie firmy.

## Jak to dziala?

  Program sprawdza kto dzisiaj:
  - Ma rejestracje WEJSCIA
  - Ale NIE ma rejestracji WYJSCIA po tym wejsciu
  - Czyli jest nadal na terenie zakladu

  Prosty przyklad:
  Jan Kowalski: WEJSCIE 06:00 → brak WYJSCIA → jest OBECNY
  Anna Nowak:   WEJSCIE 06:00 → WYJSCIE 14:00 → NIE jest obecna

---

## Kolumny w tabeli

  PRACOWNIK       Imie i nazwisko
  DZIAL           Dzial pracownika (np. Produkcja, Biuro)
  WEJSCIE         O ktorej godzinie wszedl dzisiaj
  CZAS OBECNOSCI  Ile czasu jest juz w pracy (np. 6h 23min)
  PUNKT WEJSCIA   Przez ktory czytnik wszedl (np. Portiernia)

## Przyklad: co widzisz na ekranie?

  Godzina teraz: 12:30
  ┌──────────────┬────────────┬─────────┬─────────────┬────────────┐
  │ PRACOWNIK    │ DZIAL      │ WEJSCIE │ CZAS OBEC.  │ PUNKT      │
  ├──────────────┼────────────┼─────────┼─────────────┼────────────┤
  │ Jan Kowalski │ Produkcja  │  05:58  │  6h 32min   │ Portiernia │
  │ Anna Nowak   │ Biuro      │  07:15  │  5h 15min   │ Portiernia │
  │ Marek Wisnia │ Mechanicy  │  06:05  │  6h 25min   │ Brama boczna│
  └──────────────┴────────────┴─────────┴─────────────┴────────────┘

  To znaczy ze te 3 osoby sa teraz na terenie zakladu.
  Kowalski jest najdluzej (od 5:58), Nowak najkrocej (od 7:15).

---

## Do czego to sluzy?

  - Sprawdzenie kto jest w pracy w danym momencie
  - Kontrola bezpieczenstwa (kto jest na terenie zakladu)
  - Planowanie - ilu ludzi jest na jakiej zmianie
  - Szybka odpowiedz na pytanie 'Czy Kowalski jest dzis w pracy?'

  TIP: Wlacz auto-odswiezanie (checkbox 'Auto') aby lista
  aktualizowala sie co 30 sekund bez klikania!

  UWAGA: Jesli pracownik nie odbije karty na wyjsciu,
  bedzie widoczny jako 'obecny' nawet po zakonczeniu pracy!
  Np. jesli ktos wyszedl o 14:00 ale nie odbil karty,
  o 20:00 nadal bedzie widoczny na liscie obecnych.
  Takie przypadki widac tez w zakladce Alerty.
";

        private string GetTutorialAgencje() => @"
Dwie zakladki dotycza rozliczen pracownikow z agencji pracy
tymczasowej. Agencje to firmy zewnetrzne ktore dostarczaja
pracownikow (np. Adecco, Randstad, ManpowerGroup).

## Zakladka 'Agencje' - raport miesieczny

  Jak uzyc:
  1. Wybierz agencje z listy rozwijanej (np. 'Adecco')
  2. Wybierz miesiac (np. Luty) i rok (np. 2026)
  3. Kliknij przycisk 'Generuj raport'
  4. Poczekaj az dane sie zaladuja

  Zobaczysz dla kazdego pracownika agencji:

  PRACOWNIK    Imie i nazwisko pracownika agencyjnego
  DNI PRACY    Ile dni pracowal w danym miesiacu
  NORMALNE     Godziny do 8h dziennie (stawka podstawowa)
  NADGODZINY   Godziny ponad 8h (stawka wyzsza - zwykle x1.5)
  NOCNE        Godziny miedzy 22:00-06:00 (dodatek nocny)
  SUMA         Laczna liczba godzin w miesiacu
  DO WYPLATY   Kwota do zaplaty agencji (obliczona wg stawek)

## Przyklad rozliczenia pracownika agencji:

  Pracownik: Adam Wisniak (Agencja: Adecco)
  Miesiac: Luty 2026

  ┌────────────────┬──────┬──────────┬───────────┬───────┬───────┬───────────┐
  │ PRACOWNIK      │ DNI  │ NORMALNE │ NADGODZINY│ NOCNE │ SUMA  │ DO WYPLATY│
  ├────────────────┼──────┼──────────┼───────────┼───────┼───────┼───────────┤
  │ Adam Wisniak   │  20  │  160:00  │   12:30   │ 0:00  │172:30 │ 4,250 PLN │
  └────────────────┴──────┴──────────┴───────────┴───────┴───────┴───────────┘

  Jak to liczyc?
  - 20 dni x 8h = 160h normalnych (np. po 25 PLN/h = 4,000 PLN)
  - 12.5h nadgodzin (np. po 20 PLN/h = 250 PLN)
  - Lacznie: 4,250 PLN do zaplaty agencji

  TIP: Stawki ustawiasz przyciskiem ⚙️ Stawki w gornym pasku!
  Mozesz ustawic inne stawki dla kazdej agencji.

---

## Zakladka 'Agencje' (tygodniowy) - kontrola tygodnia

  Ta zakladka jest KLUCZOWA dla kontroli pracownikow agencji.
  Pokazuje godziny dzien po dniu (Pn-Nd) dla kazdego pracownika.

  Przyklad:
  ┌────────────────┬───────┬───────┬───────┬───────┬───────┬───────┬───────┐
  │ PRACOWNIK      │  Pon  │  Wt   │  Sr   │  Czw  │  Pt   │  Sob  │  Nd   │
  ├────────────────┼───────┼───────┼───────┼───────┼───────┼───────┼───────┤
  │ Adam Wisniak   │ 🟩8:00│🟩7:45 │🟨9:30 │🟧11:00│🟩8:15 │ ---   │ ---   │
  │ Ewa Kowalczyk  │ 🟩8:00│🟩8:00 │🟩8:00 │🟥13:30│🟩8:00 │ ---   │ ---   │
  └────────────────┴───────┴───────┴───────┴───────┴───────┴───────┴───────┘

  Kolory oznaczaja:
  🟩 Zielony      Do 8 godzin (norma - wszystko OK)
  🟨 Zolty        8-10 godzin (nadgodziny, ale dopuszczalne)
  🟧 Pomaranczowy 10-12 godzin (duzo nadgodzin, uwaga!)
  🟥 Czerwony     Ponad 12 godzin - PRZEKROCZENIE LIMITU!

  UWAGA: Pracownicy agencji NIE MOGA pracowac wiecej niz
  12 godzin dziennie! To jest limit prawny. Czerwone komorki
  wymagaja natychmiastowej reakcji - skontaktuj sie z kierownikiem
  zmiany i agencja pracy!

---

## Nawigacja miedzy tygodniami

  ◀  Poprzedni tydzien
  ▶  Nastepny tydzien
  Biezacy  Wroc do aktualnego tygodnia

  TIP: Na koniec tygodnia przejrzyj widok tygodniowy aby
  upewnic sie ze nikt nie przekroczyl limitu 12h!
";

        private string GetTutorialRaport() => @"
Zakladka do generowania raportow miesiecznych. Raport to
podsumowanie godzin pracy WSZYSTKICH pracownikow za caly miesiac.
Uzywany najczesciej do rozliczen z dzialem kadr i plac.

## Jak wygenerowac raport? - krok po kroku

  1.  Przejdz do zakladki 'Raport miesieczny'
  2.  Wybierz miesiac z listy rozwijanej (np. Luty)
  3.  Wybierz rok (np. 2026)
  4.  Opcjonalnie: wybierz dzial w gornym pasku filtrow
      (jesli chcesz raport tylko dla jednego dzialu)
  5.  Kliknij przycisk 📊 Generuj
  6.  Poczekaj az dane sie zaladuja (moze trwac kilka sekund)

---

## Co zawiera raport? - opis kolumn

  Dla kazdego pracownika zobaczysz:

  PRACOWNIK     Imie i nazwisko
  DZIAL         Nazwa dzialu (np. Produkcja)
  DNI           Ile dni pracowal w danym miesiacu
  GODZINY       Suma godzin efektywnych w miesiacu
  NADGODZINY    Ile godzin ponad norme (np. ponad 8h dziennie)
  BRAKI         Ile razy brakowalo odbicia karty
  UWAGI         Szczegoly problemow

## Przyklad raportu miesiecznego:

  Raport za: LUTY 2026 / Dzial: Produkcja

  ┌────────────────┬────────────┬─────┬─────────┬───────────┬───────┬─────────────────────┐
  │ PRACOWNIK      │ DZIAL      │ DNI │ GODZINY │ NADGODZ.  │ BRAKI │ UWAGI               │
  ├────────────────┼────────────┼─────┼─────────┼───────────┼───────┼─────────────────────┤
  │ Jan Kowalski   │ Produkcja  │  20 │  164:30 │   4:30    │   0   │                     │
  │ Anna Nowak     │ Produkcja  │  18 │  148:00 │   4:00    │   2   │ 05.02 brak wyjscia  │
  │                │            │     │         │           │       │ 19.02 brak wyjscia  │
  │ Piotr Zielinski│ Produkcja  │  15 │  120:00 │   0:00    │   0   │ Urlop 10-14.02      │
  └────────────────┴────────────┴─────┴─────────┴───────────┴───────┴─────────────────────┘

  Co tu widzimy?
  - Kowalski: 20 dni, 164.5h, 4.5h nadgodzin, bez problemow ✅
  - Nowak: 18 dni, 2 braki odbicia (5 i 19 lutego nie odbila karty) ⚠️
  - Zielinski: 15 dni bo mial urlop 10-14 lutego

---

## Dodatkowe opcje - eksport i druk

  📥 Excel      Eksportuj raport do pliku CSV (otworzy sie w Excelu)
                 Plik zostanie zapisany na pulpicie z nazwa:
                 'Raport_Luty_2026_Produkcja.csv'
  🖨️ Drukuj    Wydrukuj raport (otwiera systemowe okno drukowania)
                 Raport jest sformatowany do wydruku na A4 poziomo

  TIP: Raport mozesz filtrowac po dziale - wybierz dzial
  w gornym pasku filtrow PRZED generowaniem raportu!
  Jesli chcesz raport dla calej firmy, zostaw 'Wszystkie dzialy'.

  TIP: Kolumne BRAKI sprawdzaj szczegolnie uwazenie. Jesli
  pracownik ma duzo brakow, trzeba z nim porozmawiac o
  regularnym odbijaniu karty.

  UWAGA: Raport generuje sie z danych serwera. Upewnij sie
  ze masz polaczenie z serwerem (zielona kropka w stopce).
";

        private string GetTutorialRanking() => @"
Ranking pokazuje pracownikow posortowanych wg godzin pracy
i punktualnosci. Pomaga wyroznic najlepszych pracownikow
i zidentyfikowac tych, ktorzy wymagaja rozmowy.

## Kolumny rankingu - co znaczy kazda?

  #             Pozycja w rankingu (1 = najlepszy)
  PRACOWNIK     Imie i nazwisko
  DZIAL         Nazwa dzialu
  GODZINY       Suma godzin efektywnych w wybranym okresie
  DNI PRACY     Ile dni pracowal (obecnosc)
  SR. DZIENNIE  Srednia godzin na dzien (godziny / dni)
  PUNKTUALNOSC  Procent dni BEZ spoznien
  OCENA         Gwiazdki od 1 do 3

## Przyklad rankingu:

  Okres: Luty 2026

  ┌───┬────────────────┬────────────┬─────────┬──────┬──────────┬───────┬────────┐
  │ # │ PRACOWNIK      │ DZIAL      │ GODZINY │ DNI  │SR.DZIEN. │PUNKT. │ OCENA  │
  ├───┼────────────────┼────────────┼─────────┼──────┼──────────┼───────┼────────┤
  │ 1 │ Jan Kowalski   │ Produkcja  │  168:00 │  21  │  8:00    │ 100%  │ ⭐⭐⭐ │
  │ 2 │ Ewa Wilk       │ Produkcja  │  165:30 │  21  │  7:53    │  98%  │ ⭐⭐⭐ │
  │ 3 │ Marek Lis      │ Mechanicy  │  160:00 │  20  │  8:00    │  90%  │ ⭐⭐   │
  │...│ ...            │ ...        │  ...    │ ...  │  ...     │  ...  │ ...    │
  │45 │ Tomasz Bak     │ Produkcja  │  120:00 │  15  │  8:00    │  60%  │ ❌     │
  └───┴────────────────┴────────────┴─────────┴──────┴──────────┴───────┴────────┘

  Co tu widzimy?
  - Kowalski na 1. miejscu: 21 dni, 100% punktualnosc → ⭐⭐⭐
  - Wilk na 2. miejscu: tez 21 dni, ale 98% punktualnosc
  - Bak na ostatnim: tylko 15 dni, 60% punktualnosc → ❌

---

## System ocen - jak sa przyznawane gwiazdki?

  ⭐⭐⭐  Wzorowa        Punktualnosc 95% i wiecej
                         (na 20 dni - max 1 spoznienie)
  ⭐⭐    Dobra          Punktualnosc 85-95%
                         (na 20 dni - 1 do 3 spoznien)
  ⭐      Do poprawy     Punktualnosc 70-85%
                         (na 20 dni - 3 do 6 spoznien)
  ❌      Slaba          Punktualnosc ponizej 70%
                         (na 20 dni - wiecej niz 6 spoznien)

  TIP: Ranking jest swietnym narzedziem do miesiecznych
  rozmow z pracownikami. Top 3 mozna wyroznic!

  TIP: Sortuj klikajac na naglowki kolumn. Np. kliknij
  PUNKTUALNOSC aby zobaczyc kto ma najgorsza punktualnosc.
";

        private string GetTutorialPunktualnosc() => @"
Zakladka analizuje punktualnosc kazdego pracownika.
Pokazuje ile razy kto spoznil sie i o ile minut lacznie.

## Kolumny tabeli

  PRACOWNIK     Imie i nazwisko
  DNI           Ile dni pracowal w wybranym okresie
  SPOZNIENIA    Ile razy sie spoznil (liczba dni ze spoznieniem)
  SUMA MIN      Laczna liczba minut spoznien w calym okresie
  WCZ. WYJSCIA  Ile razy wyszedl przed koncem zmiany
  %             Procent punktualnosci (im wiecej tym lepiej)
  OCENA         Ocena tekstowa (Wzorowa / Dobra / Do poprawy / Slaba)

## Przyklad tabeli punktualnosci:

  Okres: Luty 2026

  ┌────────────────┬─────┬───────────┬──────────┬───────────┬──────┬────────────┐
  │ PRACOWNIK      │ DNI │ SPOZNIENIA│ SUMA MIN │ WCZ.WYJSC.│  %   │ OCENA      │
  ├────────────────┼─────┼───────────┼──────────┼───────────┼──────┼────────────┤
  │ Jan Kowalski   │  20 │     0     │    0     │     0     │ 100% │ Wzorowa    │
  │ Anna Nowak     │  20 │     1     │    7     │     0     │  95% │ Wzorowa    │
  │ Piotr Lis      │  20 │     4     │   35     │     2     │  80% │ Do poprawy │
  │ Tomasz Bak     │  18 │     8     │   92     │     3     │  56% │ Slaba      │
  └────────────────┴─────┴───────────┴──────────┴───────────┴──────┴────────────┘

  Co tu widzimy?
  - Kowalski: ani razu sie nie spoznil → 100% → Wzorowa ✅
  - Nowak: 1 spoznienie o 7 minut → 95% → nadal Wzorowa ✅
  - Lis: 4 spoznienia (lacznie 35 minut) i 2 wczesne wyjscia → 80%
  - Bak: 8 spoznien (lacznie 1.5h!) i 3 wczesne wyjscia → 56% ❌

---

## Jak liczona jest punktualnosc?

  Punktualnosc = (dni bez spoznien / wszystkie dni) x 100%

  Przyklad:  20 dni pracy, 3 spoznienia
  Punktualnosc = (20 - 3) / 20 x 100% = 85%

  Spoznienie = wejscie po godzinie 6:05 rano
  (tolerancja 5 minut jest domyslna, czyli wejscie o 6:04 = OK,
   wejscie o 6:06 = spoznienie)

  TIP: Tolerancje mozesz zmienic w zakladce Spoznienia.
  Dostepne opcje: 0, 5, 10 lub 15 minut.

  TIP: Ta zakladka jest idealna do miesiecznych ocen pracownikow.
  Eksportuj do Excela i dolacz do dokumentacji kadrowej.
";

        private string GetTutorialSpoznienia() => @"
Zakladka do szczegolowej analizy spoznien. Tutaj widzisz
KAZDE pojedyncze spoznienie - kto, kiedy, o ile minut.

## Jak wykryc spoznienia? - krok po kroku

  1.  Przejdz do zakladki 'Spoznienia'
  2.  Wybierz miesiac i rok (np. Luty 2026)
  3.  Ustaw tolerancje (domyslnie 5 minut):
      - 0 min = kazde spoznienie po 6:00
      - 5 min = spoznienie dopiero po 6:05
      - 10 min = spoznienie dopiero po 6:10
      - 15 min = spoznienie dopiero po 6:15
  4.  Kliknij 🔍 Wykryj spoznienia
  5.  Poczekaj na wyniki

---

## Co zobaczysz? - opis kolumn

  DATA            Kiedy sie spoznil (np. 2026-02-05)
  DZIEN           Dzien tygodnia (np. Sroda)
  PRACOWNIK       Kto sie spoznil
  PLANOWANA       Oczekiwana godzina przyjscia (np. 6:00)
  RZECZYWISTA     Faktyczna godzina wejscia (np. 6:23)
  SPOZNIENIE      Ile minut spoznienia (np. 23 min)
  STATUS          ❌ nieusprawiedliwione / ✅ usprawiedliwione

## Przyklad listy spoznien:

  Tolerancja: 5 minut / Miesiac: Luty 2026

  ┌────────────┬────────┬────────────────┬──────────┬────────────┬──────────┬────────┐
  │ DATA       │ DZIEN  │ PRACOWNIK      │ PLANOW.  │ RZECZYW.   │ SPOZN.   │ STATUS │
  ├────────────┼────────┼────────────────┼──────────┼────────────┼──────────┼────────┤
  │ 2026-02-03 │ Pon    │ Tomasz Bak     │  06:00   │  🟡 06:12  │  12 min  │  ❌    │
  │ 2026-02-05 │ Sr     │ Anna Nowak     │  06:00   │  🟡 06:07  │   7 min  │  ✅    │
  │ 2026-02-07 │ Pt     │ Tomasz Bak     │  06:00   │  🟠 06:25  │  25 min  │  ❌    │
  │ 2026-02-10 │ Pon    │ Piotr Lis      │  06:00   │  🔴 06:48  │  48 min  │  ❌    │
  │ 2026-02-12 │ Sr     │ Tomasz Bak     │  06:00   │  🟡 06:08  │   8 min  │  ❌    │
  └────────────┴────────┴────────────────┴──────────┴────────────┴──────────┴────────┘

  Co tu widzimy?
  - Bak spoznia sie regularnie (3 razy w miesiacu!) - recydywista
  - Nowak spoznila sie raz o 7 min i jest juz usprawiedliwiona ✅
  - Lis mial jedno duze spoznienie - prawie godzine (48 min!)

---

## Kolory spoznien - szybka ocena powagi

  🟡 Zolty         Do 15 minut (drobne spoznienie)
  🟠 Pomaranczowy  15-30 minut (znaczace spoznienie)
  🔴 Czerwony      Ponad 30 minut (powazne spoznienie!)

---

## Usprawiedliwianie spoznien

  Niektore spoznienia maja uzasadnienie (np. wizyta u lekarza,
  korek na drodze, awaria auta). Aby usprawiedliwic:

  1.  Kliknij na wiersz spoznienia w tabeli (zaznacz go)
  2.  Kliknij przycisk ✅ Usprawiedliw
  3.  Status zmieni sie z ❌ na ✅

  Usprawiedliwione spoznienia nie wliczaja sie do oceny
  punktualnosci w zakladce Ranking.

---

## Statystyki na gorze (kafelki podsumowania)

  LICZBA SPOZNIEN     Ile spoznien lacznie w okresie (np. 23)
  SUMA MINUT          Ile minut lacznie (np. 287 min = 4h 47min)
  SREDNIA              Sredni czas spoznienia (np. 12.5 min)
  RECYDYWISCI         Ilu pracownikow ma wiecej niz 3 spoznienia

  TIP: Zwroc szczegolna uwage na RECYDYWISTOW - to osoby
  ktore spozniaja sie regularnie i wymagaja rozmowy.

  TIP: Mozesz eksportowac liste spoznien do pliku CSV
  przyciskiem 📥 i dolaczac do dokumentacji kadrowej.
";

        private string GetTutorialPrzerwy() => @"
Zakladka do kontroli przerw pracownikow. Program automatycznie
wykrywa przerwy na podstawie par WYJSCIE → WEJSCIE w ciagu dnia.
Jesli ktos wyszedl i wrocil po kilku minutach - to przerwa.

## Harmonogram przerw - co to jest?

  Po lewej stronie widzisz zaplanowane (dozwolone) przerwy:

  ┌──────────────────┬─────────────┬──────────────┐
  │ NAZWA            │ GODZINY     │ CZAS TRWANIA │
  ├──────────────────┼─────────────┼──────────────┤
  │ Sniadaniowa      │ 09:00-09:15 │  15 minut    │
  │ Obiadowa         │ 12:00-12:30 │  30 minut    │
  │ Popoludniowa     │ 15:00-15:15 │  15 minut    │
  └──────────────────┴─────────────┴──────────────┘

  To sa przerwy ktore firma oficjalnie dopuszcza.
  Mozesz dodac nowe przerwy przyciskiem ➕ Dodaj przerwe.
  Mozesz tez edytowac lub usunac istniejace.

---

## Jak analizowac przerwy? - krok po kroku

  1.  Wybierz date w gornym pasku (np. dzisiejsza data)
  2.  Kliknij 🔍 Analizuj przerwy
  3.  Program przeszuka dane i znajdzie pary:
      WYJSCIE o 09:02 → WEJSCIE o 09:18 = przerwa 16 minut
  4.  Kazda znaleziona przerwa pojawi sie w tabeli

  Program szuka przerw trwajacych od 5 do 120 minut.
  Krotsze niz 5 min = prawdopodobnie blad czytnika.
  Dluzsze niz 120 min = prawdopodobnie koniec zmiany.

---

## Przyklad wynikow analizy przerw:

  Data: 2026-02-13 (Czwartek)

  ┌────────────────┬─────────┬─────────┬──────────┬─────────────────┬────────┐
  │ PRACOWNIK      │ WYJSCIE │ WEJSCIE │ CZAS     │ PRZERWA         │ STATUS │
  ├────────────────┼─────────┼─────────┼──────────┼─────────────────┼────────┤
  │ Jan Kowalski   │  09:01  │  09:16  │  15 min  │ Sniadaniowa     │ 🟩 OK  │
  │ Jan Kowalski   │  12:02  │  12:28  │  26 min  │ Obiadowa        │ 🟩 OK  │
  │ Anna Nowak     │  09:05  │  09:25  │  20 min  │ Sniadaniowa     │ 🟨 +5  │
  │ Piotr Lis      │  10:30  │  10:52  │  22 min  │ POZA HARMONO.   │ 🟥     │
  │ Tomasz Bak     │  09:00  │  09:35  │  35 min  │ Sniadaniowa     │ 🟥 +20 │
  └────────────────┴─────────┴─────────┴──────────┴─────────────────┴────────┘

  Co tu widzimy?
  - Kowalski: 2 przerwy, obie zgodne z harmonogramem ✅
  - Nowak: przerwa sniadaniowa przedluzona o 5 min (20 zamiast 15)
  - Lis: przerwa o 10:30 - NIE MA takiej przerwy w harmonogramie! 🟥
  - Bak: przerwa sniadaniowa 35 min zamiast 15 - przekroczenie o 20 min!

---

## Statusy przerw

  🟩 OK           Przerwa w harmonogramie, nie przekroczona
  🟨 +X min       Przerwa w harmonogramie ale przedluzona o X min
  🟥              Przerwa calkowicie poza harmonogramem
  🟥 +X min       Przerwa w harmonogramie ale mocno przekroczona

---

## Filtrowanie wynikow

  Zaznacz checkbox 'Tylko poza harmonogramem' aby zobaczyc
  TYLKO problematyczne przerwy - te nieplanowane lub przekroczone.
  Ukryje to wszystkie normalne przerwy 🟩.

## Statystyki (kafelki na gorze)

  W HARMONOGRAMIE      Ile przerw bylo zgodnych z planem
  POZA HARMONOGRAMEM   Ile przerw bylo nieplanowanych
  SREDNI CZAS          Srednia dlugosc przerwy
  NAJDLUZSZA           Najdluzsza przerwa dnia (kto i ile)

  TIP: Sprawdzaj regularne te zakladke - pomaga wykryc
  pracownikow ktorzy regularnie przedluzaja przerwy.

  TIP: Jesli widzisz duzo przerw 'poza harmonogramem', moze
  warto dodac nowa przerwe do harmonogramu (np. przerwe na kawę).
";

        private string GetTutorialAlerty() => @"
System alertow automatycznie wykrywa problemy i nieprawidlowosci.
To jak alarm - informuje Cie ze cos wymaga uwagi.
Powinienes sprawdzac alerty CODZIENNIE.

## Typy alertow - co oznacza kazdy?

  🔴 Przekroczenie 12h (Agencja)
     Pracownik agencji pracowal dluzej niz 12 godzin.
     To jest NARUSZENIE prawa pracy! Wymaga natychmiastowej reakcji.
     Przyklad: Adam Wisniak (Adecco) - 13h 20min dnia 05.02.2026

  🟠 Przekroczenie 13h (Wlasny pracownik)
     Wlasny pracownik pracowal dluzej niz 13 godzin.
     Moze to byc zmeczenie lub zapomniane odbicie karty.
     Przyklad: Jan Kowalski - 13h 45min dnia 08.02.2026

  ❌ Brak wyjscia
     Pracownik odbil karte na WEJSCIU, ale NIE odbil na WYJSCIU.
     Najczesciej: zapomnial przylozyc karte wychodzac.
     Przyklad: Anna Nowak - brak wyjscia dnia 12.02.2026

  ⏰ Spoznienie (powtarzajace sie)
     Pracownik spoznil sie 3 lub wiecej razy w miesiacu.
     Wymaga rozmowy z przełożonym.
     Przyklad: Tomasz Bak - 5 spoznien w Lutym 2026

---

## Przyklad listy alertow:

  ┌──────┬────────────────────────────────────────────────┬────────────┬────────┐
  │ TYP  │ OPIS                                           │ DATA       │ STATUS │
  ├──────┼────────────────────────────────────────────────┼────────────┼────────┤
  │  🔴  │ Adam Wisniak (Adecco) - 13h20min przekroczenie │ 2026-02-05 │  NOWY  │
  │  ❌  │ Anna Nowak - brak wyjscia                      │ 2026-02-12 │  NOWY  │
  │  ❌  │ Piotr Lis - brak wyjscia                       │ 2026-02-12 │  NOWY  │
  │  ⏰  │ Tomasz Bak - 5 spoznien w Lutym                │ 2026-02-13 │  NOWY  │
  │  🟠  │ Jan Kowalski - 13h45min dlugi dzien             │ 2026-02-08 │ PRZECZYT│
  └──────┴────────────────────────────────────────────────┴────────────┴────────┘

---

## Jak korzystac z alertow? - krok po kroku

  1.  Otworz zakladke Alerty (lub patrz na kafelek ALERTY na Dashboard)
  2.  Przejrzyj nowe alerty (status: NOWY)
  3.  Rozwiaz problem (np. porozmawiaj z pracownikiem)
  4.  Kliknij ✅ Przeczytane aby oznaczyc jako obsluzone
  5.  Kliknij 🔄 Skanuj aby odswiezyc liste (szuka nowych alertow)

---

## Filtrowanie alertow

  Typ alertu:       Wybierz z listy (np. tylko 'Brak wyjscia')
  Pracownik:        Wybierz konkretna osobe
  Nieprzeczytane:   Zaznacz aby ukryc juz obsluzone alerty

  TIP: Zaznacz 'Nieprzeczytane' aby widziec TYLKO nowe problemy
  ktore jeszcze nie zostaly obsluzone. Dzieki temu nic nie umknie.

  UWAGA: Czerwone alerty 🔴 (przekroczenie 12h agencji)
  sa NAJWAZNIEJSZE - moga oznaczac naruszenie prawa pracy!
  Reaguj na nie w pierwszej kolejnosci!

  TIP: Sprawdzaj alerty codziennie rano. Jesli na Dashboard
  widzisz ALERTY: 0 - swietnie, nie ma problemow!
";

        private string GetTutorialFiltrowanie() => @"
Program oferuje wiele sposobow filtrowania danych.
Filtry sa w gornym pasku nad tabela. Mozesz je laczyc!

## Filtr dat - wybierz okres

  Od / Do       Wybierz zakres dat z kalendarza
  Dzis          Szybki filtr - tylko dzisiejsze dane
  Wczoraj       Tylko wczorajsze dane
  Tydzien       Ostatnie 7 dni
  Miesiac       Od 1. dnia biezacego miesiaca

  Przyklad uzycia:
  - Chcesz zobaczyc dane z dzisiaj → kliknij 'Dzis'
  - Chcesz caly Luty → ustaw Od: 01.02.2026, Do: 28.02.2026
  - Chcesz ostatni tydzien → kliknij 'Tydzien'

  TIP: Zmiana daty automatycznie przeladowuje dane z serwera!
  Nie musisz klikac dodatkowego przycisku.

  UWAGA: Im dluzszy okres, tym wiecej danych = dluzsze ladowanie.
  Zacznij od krotszych okresow. Caly rok moze trwac kilka sekund.

---

## Filtr dzialu - wybierz zespol

  Rozwij liste 'Dzial' i wybierz konkretny dzial.
  '-- Wszystkie dzialy --' pokazuje wszystkich pracownikow.

  Przyklady dzialow:
  - Produkcja       (pracownicy linii produkcyjnej)
  - Biuro            (administracja, ksiegowosc)
  - Mechanicy        (utrzymanie ruchu)
  - Strefa czysta    (pakowanie, etykietowanie)
  - Myjka            (mycie i dezynfekcja)

  Przyklad: chcesz zobaczyc tylko Produkcje za dzisiaj?
  1. Kliknij 'Dzis'
  2. Wybierz 'Produkcja' z listy dzialow
  3. Gotowe - widzisz tylko dane Produkcji z dzisiaj

---

## Wyszukiwarka - szybkie znajdowanie

  Wpisz w pole 'Szukaj' dowolny tekst:
  - Imie lub nazwisko pracownika (np. 'Kowalski')
  - Nazwe dzialu (np. 'Produkcja')
  - Nazwe punktu dostepu (np. 'Portiernia')

  Filtrowanie dziala NA ZYWO - wyniki zmieniaja sie
  podczas pisania. Nie musisz klikac Enter.

  Przyklady wyszukiwania:
  - Wpisz 'Kow' → wyswietli: Kowalski, Kowalczyk
  - Wpisz 'Jan' → wyswietli: Jan Kowalski, Janina Nowak
  - Wpisz 'Prod' → wyswietli tylko pracownikow z Produkcji

  Przycisk ✕ czysci wyszukiwarke (pokazuje znow wszystko).

---

## Laczenie filtrow - przyklad

  Scenariusz: 'Chce zobaczyc spoznienia Kowalskiego w Lutym'

  1.  Ustaw Od: 01.02.2026 Do: 28.02.2026
  2.  W pole Szukaj wpisz: Kowalski
  3.  Przejdz do zakladki Spoznienia
  4.  Kliknij 🔍 Wykryj
  → Widzisz tylko spoznienia Kowalskiego w Lutym!

---

## Odswiezanie danych

  ⟳ Odswiez     Recznie przeladuj dane z serwera
                  (kliknij gdy chcesz najswiezsze dane)
  Auto           Wlacz automatyczne odswiezanie co 30 sekund
                  (idealne na monitorze dyżurnym)

  TIP: Jesli dane nie laduja sie po zmianie filtrow,
  kliknij ⟳ Odswiez recznie.
";

        private string GetTutorialEksport() => @"
Dane mozna eksportowac do plikow CSV (otwieranych w Excelu)
lub drukowac na papierze. Przydatne do archiwizacji i raportow.

## Eksport rejestacji do CSV

  1.  Ustaw filtr dat (np. caly Luty 2026)
  2.  Opcjonalnie wybierz dzial
  3.  Kliknij 📥 w gornym pasku filtrow
  4.  Wybierz miejsce zapisu (domyslnie Pulpit)
  5.  Plik zostanie zapisany jako CSV

  Format pliku: Data; Godzina; Typ; Pracownik; Dzial; Punkt
  Przyklad wiersza w pliku:
  2026-02-13; 06:02:15; WEJSCIE; Jan Kowalski; Produkcja; Portiernia

---

## Eksport raportu miesiecznego

  1.  Przejdz do zakladki 'Raport miesieczny'
  2.  Wygeneruj raport (miesiac, rok, Generuj)
  3.  Kliknij 📥 Excel
  4.  Wybierz miejsce zapisu

  Plik bedzie zawierac: Pracownik, Dzial, Dni, Godziny,
  Nadgodziny, Braki, Uwagi

---

## Eksport rozliczenia agencji

  1.  Przejdz do zakladki 'Agencje'
  2.  Wygeneruj raport agencji
  3.  Kliknij 📥 Export Excel
  4.  Wybierz miejsce zapisu

  Plik bedzie zawierac: Pracownik, Dni, Normalne, Nadgodziny,
  Nocne, Suma godzin, Kwota do wyplaty

  TIP: Ten plik mozna wyslac bezposrednio do agencji pracy
  jako podstawe do faktury!

---

## Jak otworzyc plik CSV w Excelu?

  Sposob 1 (prosty):
  - Kliknij dwukrotnie na plik CSV → otworzy sie w Excelu

  Sposob 2 (jesli dane sa w jednej kolumnie):
  - Otworz Excel → Dane → Z pliku tekstowego/CSV
  - Wybierz plik → Separator: Srednik (;) → Zaladuj

  TIP: Pliki CSV uzywaja srednika (;) jako separatora
  i kodowania UTF-8 (polskie znaki beda poprawne).

---

## Drukowanie raportow

  W zakladce 'Raport Mies.' kliknij 🖨️ Drukuj.
  Otworzy sie systemowe okno drukowania:
  1.  Wybierz drukarke z listy
  2.  Ustaw orientacje: Pozioma (lepiej dla szerokich tabel)
  3.  Kliknij Drukuj

  Raport jest sformatowany do wydruku na A4 w orientacji
  poziomej. Zawiera naglowek z data, dzialem i okresem.

  UWAGA: Przed drukowaniem upewnij sie ze raport wyglada
  poprawnie na ekranie. Druk kosztuje papier i tusz!
";

        private string GetTutorialDiagnostyka() => @"
Narzedzie diagnostyczne pomaga gdy program nie moze polaczyc
sie z serwerem SQL. Uruchom je gdy widzisz bledy lub
czerwona kropke 🔴 w stopce okna.

## Kiedy uruchamic diagnostyke?

  - Program pokazuje 'Brak polaczenia' w stopce
  - Dane nie laduja sie lub sa puste
  - Pojawia sie komunikat o bledzie SQL
  - Chcesz sprawdzic stan serwera bazy danych

## Jak uruchomic diagnostyke?

  1.  Spojrz na STOPKE okna (dolny pasek)
  2.  Kliknij ikone 🔧 (po prawej stronie tekstu statusu)
  3.  Poczekaj az testy sie zakoncza (moze trwac 10-30 sekund)
  4.  Otworzy sie okno z wynikami

---

## Co sprawdza diagnostyka? - 9 testow

  TEST 1: Ping serwera
  Sprawdza czy komputer serwera (192.168.0.23) odpowiada w sieci.
  PASS = serwer jest wlaczony i widoczny w sieci
  FAIL = serwer wylaczony lub problem z siecia

  TEST 2: Port TCP 1433
  Sprawdza czy SQL Server nasluchuje na standardowym porcie.
  PASS = SQL Server jest uruchomiony i przyjmuje polaczenia
  FAIL = SQL Server nie dziala lub port jest zablokowany

  TEST 3: Polaczenie SQL
  Probuje polaczyc sie do bazy danych z loginem i haslem.
  PASS = polaczenie udane, baza dostepna
  FAIL = zly login/haslo lub baza nie istnieje

  TEST 4: Widoki i tabele bazy
  Sprawdza czy tabele UNICARD (V_RCINE_EMPLOYEES itd.) istnieja.
  PASS = struktura bazy jest poprawna
  FAIL = baza uszkodzona lub inna wersja UNICARD

  TEST 5: Licznosc danych
  Liczy ile jest rekordow (pracownikow, rejestracji, grup).
  Pokazuje czy baza zawiera dane.

  TEST 6: Probki danych
  Wyswietla kilka przykladowych rekordow z bazy.
  Pomaga zweryfikowac czy dane sa sensowne.

  TEST 7: Zapytania aplikacji
  Uruchamia te same zapytania co program.
  Jesli tu dzialaja a w programie nie → problem w kodzie.

  TEST 8: Uprawnienia
  Sprawdza czy uzytkownik SQL ma wystarczajace uprawnienia.

  TEST 9: Status serwera
  Informacje o wersji SQL Server, RAM, CPU.

---

## Co robic z wynikiem?

  1.  Kliknij 'Kopiuj do schowka' (skopiuje caly raport)
  2.  Otworz email lub komunikator
  3.  Wklej raport (Ctrl+V) i wyslij do administratora IT
  4.  Raport zawiera WSZYSTKO co IT potrzebuje do diagnozy

  Mozesz tez kliknac 'Zapisz do pliku' aby zachowac raport
  na dysku jako plik tekstowy.

---

## Najczescsze problemy i rozwiazania

  Problem: Wszystkie testy FAIL
  → Serwer jest wylaczony lub odlaczony od sieci
  → Rozwiazanie: poproś IT o wlaczenie serwera

  Problem: Ping OK, Port FAIL
  → SQL Server nie dziala na serwerze
  → Rozwiazanie: restart uslogi SQL Server na serwerze

  Problem: Ping OK, Port OK, Polaczenie FAIL
  → Zle haslo lub baza nie istnieje
  → Rozwiazanie: sprawdzic haslo z administratorem

  Problem: Polaczenie OK, ale dane FAIL
  → Baza jest pusta lub uszkodzona
  → Rozwiazanie: sprawdzic system UNICARD na serwerze

  TIP: Najczesciej problem to wylaczony serwer lub
  firewall blokujacy port 1433 po aktualizacji Windows.
";

        private string GetTutorialSkroty() => @"
Porady, triki i skroty dla zaawansowanych uzytkownikow.
Jesli juz znasz podstawy - ta sekcja pomoze Ci pracowac szybciej.

## Przyciski w gornym pasku - co robi kazdy?

  📖 Pomoc         Ten tutorial (wlasnie go czytasz!)
  📊 Timeline      Wizualny harmonogram dnia na osi czasu
                    (pokazuje pracownikow jako paski czasowe)
  📈 Statystyki    Wykresy i analizy statystyczne
                    (wykresy slupkowe, kolowe, trendy miesieczne)
  💳 Karty         Zarzadzanie kartami RCP pracownikow
                    (przypisywanie kart, blokowanie, historia)
  🖨️ Karta RCP    Drukowanie karty czasu pracy jednego pracownika
                    (formularz do podpisania przez pracownika)
  ⚙️ Stawki        Ustawienia stawek godzinowych agencji
                    (stawka normalna, nadgodziny, nocne)

---

## Dwuklik na wiersz tabeli - szczegoly

  W wiekszosci tabel mozesz kliknac DWUKROTNIE na wiersz
  aby zobaczyc szczegolowe informacje:

  Zakladka Rejestracje     → wszystkie odbicia danego dnia
  Zakladka Godziny pracy   → rozklad: wejscia, przerwy, wyjscia
  Zakladka Raport mies.    → pelne dane pracownika za miesiac
  Zakladka Ranking          → historia godzin i punktualnosci

  Przyklad: dwuklik na Kowalskiego w Godzinach pracy pokaze:
  ┌──────────┬─────────┬──────────────────────────────┐
  │ 05:58    │ WEJSCIE │ Portiernia - czytnik glowny  │
  │ 09:01    │ WYJSCIE │ Produkcja - czytnik WY       │
  │ 09:16    │ WEJSCIE │ Produkcja - czytnik WE       │
  │ 12:00    │ WYJSCIE │ Produkcja - czytnik WY       │
  │ 12:28    │ WEJSCIE │ Produkcja - czytnik WE       │
  │ 14:02    │ WYJSCIE │ Portiernia - czytnik glowny  │
  └──────────┴─────────┴──────────────────────────────┘

---

## Kolory w tabelach - uniwersalny kod

  Kolory sa takie same we WSZYSTKICH zakladkach:

  🟩 Zielony tlo   = wszystko OK, norma
  🟨 Zolty tlo     = do sprawdzenia (np. male nadgodziny)
  🟧 Pomaranczowy  = wymaga uwagi (np. duzo nadgodzin)
  🟥 Czerwony tlo  = PROBLEM / przekroczenie limitu
  ⬜ Szary tlo     = brak danych lub nieaktywny

  Ogolna zasada: im ciemniejszy/czerwienszy kolor,
  tym powazniejszy problem. Zielony = spokoj.

---

## Najczesciej uzywane scenariusze

  CODZIENNIE RANO:
  1.  Otworz program → Dashboard
  2.  Sprawdz ile osob jest w pracy
  3.  Sprawdz kafelek ALERTY → jesli > 0 → otworz Alerty
  4.  Obsluz nowe alerty (brak wyjsc, przekroczenia)

  CO TYDZIEN (piatek):
  1.  Przejdz do Agencje (tygodniowy)
  2.  Sprawdz czy nikt nie przekroczyl 12h
  3.  Przejrzyj spoznienia za tydzien

  CO MIESIAC (ostatni dzien):
  1.  Raport miesieczny → Generuj za caly miesiac
  2.  Eksportuj do Excela 📥
  3.  Agencje → Generuj rozliczenie → Eksportuj
  4.  Ranking → sprawdz najlepszych i najgorszych

---

## Klawiatura

  Ctrl+F       Szybkie szukanie (aktywuje pole Szukaj)
  F5            Odswiez dane z serwera
  Escape        Zamknij otwarte okno dialogowe

  TIP: Na co dzien najwazniejsze sa Dashboard i Alerty.
  Raporty miesieczne generuj na koniec kazdego miesiaca.
  Raport agencji wyslij do agencji jako podstawe faktury.

  TIP: Jesli masz wiele monitorow, zostaw program otwarty
  na jednym z Dashboard i wlaczonym Auto-odswiezaniem.
  Masz wtedy ciagle podglad na obecnosc pracownikow.
";

        #endregion

        #region Diagnostyka SQL

        private void BtnDiagnostyka_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var sb = new StringBuilder();
            int testNr = 0;
            int passed = 0;
            int failed = 0;

            sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║           DIAGNOSTYKA POŁĄCZENIA SQL - UNICARD RCP          ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"  Data raportu:     {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Komputer:         {Environment.MachineName}");
            sb.AppendLine($"  Użytkownik Win:   {Environment.UserName}");
            sb.AppendLine($"  OS:               {Environment.OSVersion}");
            sb.AppendLine($"  .NET Runtime:     {Environment.Version}");
            sb.AppendLine($"  App UserID:       {App.UserID}");
            sb.AppendLine($"  App UserName:     {App.UserFullName}");
            sb.AppendLine();

            // Parsuj connection string bezpiecznie
            var csBuilder = new SqlConnectionStringBuilder(_connectionString);
            sb.AppendLine("  Connection String:");
            sb.AppendLine($"    Server:         {csBuilder.DataSource}");
            sb.AppendLine($"    Database:       {csBuilder.InitialCatalog}");
            sb.AppendLine($"    User:           {csBuilder.UserID}");
            sb.AppendLine($"    Password:       {"*".PadRight(csBuilder.Password.Length, '*')}");
            sb.AppendLine($"    Timeout:        {csBuilder.ConnectTimeout}s");
            sb.AppendLine();

            // ─── 1. PING SERWERA ───
            sb.AppendLine($"── TEST {++testNr}: PING SERWERA ──");
            try
            {
                var host = csBuilder.DataSource.Split('\\')[0];
                using (var ping = new System.Net.NetworkInformation.Ping())
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var reply = ping.Send(host, 3000);
                    sw.Stop();
                    if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                    {
                        sb.AppendLine($"  ✅ PASS  Ping {host} → {reply.RoundtripTime}ms (TTL={reply.Options?.Ttl})");
                        sb.AppendLine($"           IP: {reply.Address}");
                        passed++;
                    }
                    else
                    {
                        sb.AppendLine($"  ❌ FAIL  Ping {host} → {reply.Status}");
                        failed++;
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ⚠️ SKIP  Ping niedostępny: {ex.Message}");
            }
            sb.AppendLine();

            // ─── 2. PORT TCP 1433 ───
            sb.AppendLine($"── TEST {++testNr}: PORT TCP 1433 ──");
            try
            {
                var host = csBuilder.DataSource.Split('\\')[0];
                using (var tcp = new System.Net.Sockets.TcpClient())
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var connectTask = tcp.ConnectAsync(host, 1433);
                    if (connectTask.Wait(3000))
                    {
                        sw.Stop();
                        sb.AppendLine($"  ✅ PASS  Port 1433 otwarty ({sw.ElapsedMilliseconds}ms)");
                        passed++;
                    }
                    else
                    {
                        sb.AppendLine($"  ❌ FAIL  Port 1433 timeout (>3s) - firewall?");
                        failed++;
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ❌ FAIL  Port 1433: {ex.Message}");
                failed++;
            }
            sb.AppendLine();

            // ─── 3. SQL CONNECTION ───
            sb.AppendLine($"── TEST {++testNr}: POŁĄCZENIE SQL ──");
            string serverVersion = "";
            bool connectionOk = false;
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    conn.Open();
                    sw.Stop();
                    connectionOk = true;
                    serverVersion = conn.ServerVersion;
                    sb.AppendLine($"  ✅ PASS  Połączono w {sw.ElapsedMilliseconds}ms");
                    sb.AppendLine($"           SQL Server wersja: {conn.ServerVersion}");
                    sb.AppendLine($"           Baza danych:       {conn.Database}");
                    sb.AppendLine($"           WorkstationId:     {conn.WorkstationId}");
                    sb.AppendLine($"           PacketSize:        {conn.PacketSize}");
                    passed++;

                    // Dodatkowe info o serwerze
                    try
                    {
                        using (var cmd = new SqlCommand("SELECT @@SERVERNAME, @@SERVICENAME, SERVERPROPERTY('Edition'), SERVERPROPERTY('ProductLevel'), SYSTEM_USER, DB_NAME()", conn))
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                sb.AppendLine($"           ServerName:        {reader.GetValue(0)}");
                                sb.AppendLine($"           ServiceName:       {reader.GetValue(1)}");
                                sb.AppendLine($"           Edition:           {reader.GetValue(2)}");
                                sb.AppendLine($"           ProductLevel:      {reader.GetValue(3)}");
                                sb.AppendLine($"           SQL User:          {reader.GetValue(4)}");
                                sb.AppendLine($"           Current DB:        {reader.GetValue(5)}");
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  ❌ FAIL  {ex.Message}");
                if (ex.InnerException != null)
                    sb.AppendLine($"           Inner: {ex.InnerException.Message}");
                sb.AppendLine($"           Typ wyjątku: {ex.GetType().FullName}");
                sb.AppendLine($"           HResult: 0x{ex.HResult:X8}");
                if (ex is SqlException sqlEx)
                {
                    sb.AppendLine($"           SQL Error Number: {sqlEx.Number}");
                    sb.AppendLine($"           SQL Error Class:  {sqlEx.Class}");
                    sb.AppendLine($"           SQL State:        {sqlEx.State}");
                    foreach (SqlError err in sqlEx.Errors)
                        sb.AppendLine($"           → [{err.Number}] {err.Message} (Line {err.LineNumber}, Proc: {err.Procedure})");
                }
                failed++;
            }
            sb.AppendLine();

            if (!connectionOk)
            {
                sb.AppendLine("══ DIAGNOSTYKA PRZERWANA - BRAK POŁĄCZENIA Z SERWEREM ══");
                sb.AppendLine();
                sb.AppendLine("Możliwe przyczyny:");
                sb.AppendLine("  • Serwer SQL jest wyłączony");
                sb.AppendLine("  • Zła nazwa serwera / instancji");
                sb.AppendLine("  • Firewall blokuje port 1433");
                sb.AppendLine("  • SQL Server Browser nie działa (potrzebny dla named instances)");
                sb.AppendLine("  • Złe hasło lub login");
                sb.AppendLine();
                sb.AppendLine($"Wynik: {passed} PASS / {failed} FAIL");
                PokazDiagnostyke(sb.ToString());
                return;
            }

            // ─── 4. WIDOKI - ISTNIENIE I SCHEMAT ───
            var widoki = new[]
            {
                "V_RCINEG_EMPLOYEES_GROUPS",
                "V_RCINE_EMPLOYEES",
                "V_KDINAR_ALL_REGISTRATIONS",
                "V_KDINEC_EMPLOYEES_CARDS",
                "T_KDCAC_CARDS"
            };

            sb.AppendLine($"── TEST {++testNr}: WIDOKI I TABELE ──");
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();

                foreach (var widok in widoki)
                {
                    try
                    {
                        using (var cmd = new SqlCommand($"SELECT TOP 1 * FROM {widok}", conn))
                        {
                            cmd.CommandTimeout = 10;
                            var sw = System.Diagnostics.Stopwatch.StartNew();
                            using (var reader = cmd.ExecuteReader())
                            {
                                sw.Stop();
                                sb.AppendLine($"  ✅ PASS  {widok} ({sw.ElapsedMilliseconds}ms, {reader.FieldCount} kolumn)");
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var colName = reader.GetName(i);
                                    var colType = reader.GetFieldType(i);
                                    var dbType = reader.GetDataTypeName(i);
                                    string sampleVal = "";
                                    if (reader.HasRows && reader.Read())
                                        sampleVal = reader.IsDBNull(i) ? "<NULL>" : TruncateValue(reader.GetValue(i)?.ToString(), 40);
                                    sb.AppendLine($"           [{i,2}] {colName,-45} {dbType,-15} (.NET: {colType.Name,-10}) = {sampleVal}");
                                }
                                passed++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  ❌ FAIL  {widok}: {ex.Message}");
                        failed++;
                    }
                    sb.AppendLine();
                }

                // ─── 5. LICZNOŚĆ DANYCH ───
                sb.AppendLine($"── TEST {++testNr}: LICZNOŚĆ DANYCH ──");

                var countQueries = new Dictionary<string, string>
                {
                    ["Grupy (ogółem)"] = "SELECT COUNT(*) FROM V_RCINEG_EMPLOYEES_GROUPS",
                    ["Grupy (unikalne)"] = "SELECT COUNT(DISTINCT RCINEG_EMPLOYEE_GROUP_NAME) FROM V_RCINEG_EMPLOYEES_GROUPS WHERE RCINEG_EMPLOYEE_GROUP_NAME IS NOT NULL",
                    ["Pracownicy (typ=1, aktywni)"] = "SELECT COUNT(*) FROM V_RCINE_EMPLOYEES WHERE RCINE_EMPLOYEE_TYPE = 1",
                    ["Pracownicy (wszyscy)"] = "SELECT COUNT(*) FROM V_RCINE_EMPLOYEES",
                    ["Rejestracje (dziś)"] = $"SELECT COUNT(*) FROM V_KDINAR_ALL_REGISTRATIONS WHERE KDINAR_REGISTRTN_DATETIME >= '{DateTime.Today:yyyy-MM-dd}' AND KDINAR_EMPLOYEE_ID IS NOT NULL",
                    ["Rejestracje (wczoraj)"] = $"SELECT COUNT(*) FROM V_KDINAR_ALL_REGISTRATIONS WHERE KDINAR_REGISTRTN_DATETIME >= '{DateTime.Today.AddDays(-1):yyyy-MM-dd}' AND KDINAR_REGISTRTN_DATETIME < '{DateTime.Today:yyyy-MM-dd}' AND KDINAR_EMPLOYEE_ID IS NOT NULL",
                    ["Rejestracje (ten miesiąc)"] = $"SELECT COUNT(*) FROM V_KDINAR_ALL_REGISTRATIONS WHERE KDINAR_REGISTRTN_DATETIME >= '{new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1):yyyy-MM-dd}' AND KDINAR_EMPLOYEE_ID IS NOT NULL",
                    ["Rejestracje (ogółem w bazie)"] = "SELECT COUNT(*) FROM V_KDINAR_ALL_REGISTRATIONS WHERE KDINAR_EMPLOYEE_ID IS NOT NULL",
                    ["Karty (aktywne)"] = "SELECT COUNT(*) FROM V_KDINEC_EMPLOYEES_CARDS WHERE KDINEC_DATETIME_TO IS NULL",
                };

                foreach (var kv in countQueries)
                {
                    try
                    {
                        using (var cmd = new SqlCommand(kv.Value, conn))
                        {
                            cmd.CommandTimeout = 15;
                            var count = Convert.ToInt64(cmd.ExecuteScalar());
                            string status = count > 0 ? "✅" : "⚠️";
                            sb.AppendLine($"  {status} {kv.Key,-35} = {count:N0}");
                            if (count > 0) passed++; else failed++;
                        }
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  ❌ {kv.Key,-35} = BŁĄD: {ex.Message}");
                        failed++;
                    }
                }
                sb.AppendLine();

                // ─── 6. PRÓBKA REJESTRACJI ───
                sb.AppendLine($"── TEST {++testNr}: PRÓBKA REJESTRACJI (5 ostatnich) ──");
                try
                {
                    using (var cmd = new SqlCommand(@"SELECT TOP 5 * FROM V_KDINAR_ALL_REGISTRATIONS
                        WHERE KDINAR_EMPLOYEE_ID IS NOT NULL ORDER BY KDINAR_REGISTRTN_DATETIME DESC", conn))
                    {
                        cmd.CommandTimeout = 10;
                        using (var reader = cmd.ExecuteReader())
                        {
                            int row = 0;
                            while (reader.Read())
                            {
                                row++;
                                sb.AppendLine($"  --- Rekord {row} ---");
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var val = reader.IsDBNull(i) ? "<NULL>" : reader.GetValue(i)?.ToString();
                                    var realType = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.GetType().Name;
                                    sb.AppendLine($"    {reader.GetName(i),-45} [{reader.GetDataTypeName(i),-12} → .NET {realType,-10}] = {TruncateValue(val, 50)}");
                                }
                            }
                            if (row == 0)
                            {
                                sb.AppendLine("  ⚠️ BRAK DANYCH w widoku rejestracji!");
                                failed++;
                            }
                            else
                            {
                                sb.AppendLine($"  ✅ Pobrano {row} rekordów");
                                passed++;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  ❌ FAIL: {ex.Message}");
                    failed++;
                }
                sb.AppendLine();

                // ─── 7. PRÓBKA PRACOWNIKÓW ───
                sb.AppendLine($"── TEST {++testNr}: PRÓBKA PRACOWNIKÓW (3 pierwsze) ──");
                try
                {
                    using (var cmd = new SqlCommand(@"SELECT TOP 3 * FROM V_RCINE_EMPLOYEES WHERE RCINE_EMPLOYEE_TYPE = 1
                        ORDER BY RCINE_EMPLOYEE_SURNAME", conn))
                    {
                        cmd.CommandTimeout = 10;
                        using (var reader = cmd.ExecuteReader())
                        {
                            int row = 0;
                            while (reader.Read())
                            {
                                row++;
                                sb.AppendLine($"  --- Pracownik {row} ---");
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    var val = reader.IsDBNull(i) ? "<NULL>" : reader.GetValue(i)?.ToString();
                                    var realType = reader.IsDBNull(i) ? "NULL" : reader.GetValue(i)?.GetType().Name;
                                    sb.AppendLine($"    {reader.GetName(i),-45} [{reader.GetDataTypeName(i),-12} → .NET {realType,-10}] = {TruncateValue(val, 50)}");
                                }
                            }
                            if (row == 0) { sb.AppendLine("  ⚠️ BRAK PRACOWNIKÓW (typ=1)!"); failed++; }
                            else { passed++; }
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  ❌ FAIL: {ex.Message}");
                    failed++;
                }
                sb.AppendLine();

                // ─── 8. TEST ZAPYTAŃ UŻYWANYCH W APLIKACJI ───
                sb.AppendLine($"── TEST {++testNr}: ZAPYTANIA APLIKACJI ──");

                // Test zapytania LoadGrupy
                sb.AppendLine("  [LoadGrupy]");
                try
                {
                    using (var cmd = new SqlCommand(@"SELECT DISTINCT RCINEG_EMPLOYEE_GROUP_ID, RCINEG_EMPLOYEE_GROUP_NAME
                        FROM V_RCINEG_EMPLOYEES_GROUPS WHERE RCINEG_EMPLOYEE_GROUP_NAME IS NOT NULL
                        ORDER BY RCINEG_EMPLOYEE_GROUP_NAME", conn))
                    {
                        cmd.CommandTimeout = 10;
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        using (var reader = cmd.ExecuteReader())
                        {
                            int cnt = 0;
                            while (reader.Read()) cnt++;
                            sw.Stop();
                            sb.AppendLine($"  ✅ PASS  {cnt} grup, {sw.ElapsedMilliseconds}ms");
                            passed++;
                        }
                    }
                }
                catch (Exception ex) { sb.AppendLine($"  ❌ FAIL  {ex.Message}"); failed++; }

                // Test zapytania LoadPracownicy
                sb.AppendLine("  [LoadPracownicy]");
                try
                {
                    using (var cmd = new SqlCommand(@"SELECT RCINE_EMPLOYEE_ID, RCINE_EMPLOYEE_NAME, RCINE_EMPLOYEE_SURNAME,
                        RCINE_EMPLOYEE_GROUP_ID, RCINE_EMPLOYEE_GROUP_NAME FROM V_RCINE_EMPLOYEES
                        WHERE RCINE_EMPLOYEE_TYPE = 1 ORDER BY RCINE_EMPLOYEE_SURNAME, RCINE_EMPLOYEE_NAME", conn))
                    {
                        cmd.CommandTimeout = 10;
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        using (var reader = cmd.ExecuteReader())
                        {
                            int cnt = 0;
                            while (reader.Read()) cnt++;
                            sw.Stop();
                            sb.AppendLine($"  ✅ PASS  {cnt} pracowników, {sw.ElapsedMilliseconds}ms");
                            passed++;
                        }
                    }
                }
                catch (Exception ex) { sb.AppendLine($"  ❌ FAIL  {ex.Message}"); failed++; }

                // Test zapytania LoadAllData
                sb.AppendLine("  [LoadAllData]");
                try
                {
                    using (var cmd = new SqlCommand(@"SELECT TOP 10 KDINAR_REGISTRTN_DATETIME, KDINAR_REGISTRTN_TYPE,
                        KDINAR_EMPLOYEE_ID, KDINAR_EMPLOYEE_NAME, KDINAR_EMPLOYEE_SURNAME,
                        KDINAR_EMPLOYEE_GROUP_ID, KDINAR_ACCESS_POINT_NAME
                        FROM V_KDINAR_ALL_REGISTRATIONS
                        WHERE KDINAR_REGISTRTN_DATETIME >= @DataOd AND KDINAR_REGISTRTN_DATETIME < @DataDo
                        AND KDINAR_EMPLOYEE_ID IS NOT NULL ORDER BY KDINAR_REGISTRTN_DATETIME DESC", conn))
                    {
                        cmd.Parameters.AddWithValue("@DataOd", DateTime.Today);
                        cmd.Parameters.AddWithValue("@DataDo", DateTime.Today.AddDays(1));
                        cmd.CommandTimeout = 10;
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        using (var reader = cmd.ExecuteReader())
                        {
                            int cnt = 0;
                            while (reader.Read()) cnt++;
                            sw.Stop();
                            sb.AppendLine($"  ✅ PASS  {cnt} rejestracji (TOP 10), {sw.ElapsedMilliseconds}ms");
                            passed++;
                        }
                    }
                }
                catch (Exception ex) { sb.AppendLine($"  ❌ FAIL  {ex.Message}"); failed++; }

                // Test starych kolumn (które usunęliśmy)
                sb.AppendLine("  [Opcjonalne kolumny]");
                var optCols = new[] { "KDINAR_CARD_NUMBER", "KDINAR_DEVICE_NAME", "KDINAR_REGISTRTN_MODE" };
                foreach (var col in optCols)
                {
                    try
                    {
                        using (var cmd = new SqlCommand($"SELECT TOP 1 {col} FROM V_KDINAR_ALL_REGISTRATIONS", conn))
                        {
                            cmd.CommandTimeout = 5;
                            using (var reader = cmd.ExecuteReader())
                            {
                                string typ = reader.GetDataTypeName(0);
                                string val = "(pusta tabela)";
                                if (reader.Read())
                                    val = reader.IsDBNull(0) ? "<NULL>" : $"{reader.GetValue(0)} ({reader.GetValue(0)?.GetType().Name})";
                                sb.AppendLine($"  ✅       {col,-35} typ={typ,-12} wartość={val}");
                            }
                        }
                    }
                    catch (Exception ex) { sb.AppendLine($"  ⚠️       {col,-35} NIE ISTNIEJE ({ex.Message})"); }
                }
                sb.AppendLine();

                // ─── 9. UPRAWNIENIA UŻYTKOWNIKA SQL ───
                sb.AppendLine($"── TEST {++testNr}: UPRAWNIENIA SQL ──");
                try
                {
                    using (var cmd = new SqlCommand(@"SELECT dp.name, dp.type_desc,
                        IS_SRVROLEMEMBER('sysadmin', dp.name) as is_sysadmin,
                        IS_SRVROLEMEMBER('dbcreator', dp.name) as is_dbcreator
                        FROM sys.server_principals dp WHERE dp.name = SYSTEM_USER", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                sb.AppendLine($"  Login:     {reader.GetValue(0)}");
                                sb.AppendLine($"  Typ:       {reader.GetValue(1)}");
                                sb.AppendLine($"  sysadmin:  {(Convert.ToInt32(reader.GetValue(2)) == 1 ? "TAK" : "NIE")}");
                                sb.AppendLine($"  dbcreator: {(Convert.ToInt32(reader.GetValue(3)) == 1 ? "TAK" : "NIE")}");
                                passed++;
                            }
                        }
                    }
                }
                catch (Exception ex) { sb.AppendLine($"  ⚠️ Nie można sprawdzić: {ex.Message}"); }
                sb.AppendLine();

                // ─── 10. STATUS SERWERA SQL ───
                sb.AppendLine($"── TEST {++testNr}: STATUS SERWERA ──");
                try
                {
                    using (var cmd = new SqlCommand(@"SELECT
                        (SELECT COUNT(*) FROM sys.dm_exec_sessions) as sessions,
                        (SELECT sqlserver_start_time FROM sys.dm_os_sys_info) as start_time,
                        (SELECT physical_memory_kb/1024 FROM sys.dm_os_sys_info) as ram_mb,
                        (SELECT cpu_count FROM sys.dm_os_sys_info) as cpus,
                        DB_NAME() as current_db,
                        (SELECT SUM(size)*8/1024 FROM sys.database_files) as db_size_mb", conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                sb.AppendLine($"  Aktywne sesje:    {reader.GetValue(0)}");
                                sb.AppendLine($"  SQL uruchomiony:  {reader.GetValue(1)}");
                                sb.AppendLine($"  RAM serwera:      {reader.GetValue(2)} MB");
                                sb.AppendLine($"  CPU:              {reader.GetValue(3)} rdzeni");
                                sb.AppendLine($"  Baza danych:      {reader.GetValue(4)}");
                                sb.AppendLine($"  Rozmiar bazy:     {reader.GetValue(5)} MB");
                                passed++;
                            }
                        }
                    }
                }
                catch (Exception ex) { sb.AppendLine($"  ⚠️ {ex.Message}"); }
            }

            // ─── PODSUMOWANIE ───
            sb.AppendLine();
            sb.AppendLine("── STAN APLIKACJI (pamięć) ──");
            sb.AppendLine($"  Grupy załadowane:       {_grupy?.Count ?? 0}");
            sb.AppendLine($"  Pracownicy załadowani:  {_pracownicy?.Count ?? 0}");
            sb.AppendLine($"  Rejestracje w pamięci:  {_wszystkieRejestracje?.Count ?? 0}");
            sb.AppendLine($"  Zakres dat:             {dpOd.SelectedDate:yyyy-MM-dd} → {dpDo.SelectedDate:yyyy-MM-dd}");
            sb.AppendLine();
            sb.AppendLine("╔══════════════════════════════════════════════════════════════╗");
            sb.AppendLine($"║  WYNIK:  {passed} PASS  /  {failed} FAIL                                  ║");
            if (failed == 0)
                sb.AppendLine("║  STATUS: ✅ WSZYSTKO OK                                     ║");
            else
                sb.AppendLine("║  STATUS: ❌ WYKRYTO PROBLEMY                                ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════════╝");

            PokazDiagnostyke(sb.ToString());
        }

        private static string TruncateValue(string val, int max)
        {
            if (string.IsNullOrEmpty(val)) return "";
            return val.Length <= max ? val : val.Substring(0, max) + "...";
        }

        private void PokazDiagnostyke(string raport)
        {
            var window = new Window
            {
                Title = "Diagnostyka SQL - UNICARD RCP",
                Width = 950,
                Height = 750,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(26, 32, 44))
            };
            WindowIconHelper.SetIcon(window);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var textBox = new TextBox
            {
                Text = raport,
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap
            };
            Grid.SetRow(textBox, 0);
            grid.Children.Add(textBox);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 10, 16, 10)
            };
            Grid.SetRow(btnPanel, 1);

            var btnHelp = new Button
            {
                Content = "Jak korzystac?",
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(237, 137, 54)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnHelp.Click += (s, ev) => PokazTutorial();

            var btnCopy = new Button
            {
                Content = "Kopiuj do schowka",
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(66, 153, 225)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCopy.Click += (s, ev) =>
            {
                Clipboard.SetText(raport);
                btnCopy.Content = "Skopiowano!";
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (t, te) => { btnCopy.Content = "Kopiuj do schowka"; timer.Stop(); };
                timer.Start();
            };

            var btnSave = new Button
            {
                Content = "Zapisz do pliku",
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 13,
                Background = new SolidColorBrush(Color.FromRgb(72, 187, 120)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnSave.Click += (s, ev) =>
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt",
                    FileName = $"diagnostyka_sql_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };
                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllText(dlg.FileName, raport, Encoding.UTF8);
                    btnSave.Content = "Zapisano!";
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (t, te) => { btnSave.Content = "Zapisz do pliku"; timer.Stop(); };
                    timer.Start();
                }
            };

            var btnClose = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(20, 8, 20, 8),
                FontSize = 13,
                Background = new SolidColorBrush(Color.FromRgb(113, 128, 150)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnClose.Click += (s, ev) => window.Close();

            btnPanel.Children.Add(btnHelp);
            btnPanel.Children.Add(btnCopy);
            btnPanel.Children.Add(btnSave);
            btnPanel.Children.Add(btnClose);
            grid.Children.Add(btnPanel);

            window.Content = grid;
            window.ShowDialog();
        }

        private void PokazTutorial()
        {
            var tutorial = new StringBuilder();
            tutorial.AppendLine("╔══════════════════════════════════════════════════════════════════════════════╗");
            tutorial.AppendLine("║              TUTORIAL - DIAGNOSTYKA SQL UNICARD RCP                        ║");
            tutorial.AppendLine("║              Jak czytac raport i rozwiazywac problemy                      ║");
            tutorial.AppendLine("╚══════════════════════════════════════════════════════════════════════════════╝");
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  CO TO JEST?");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Diagnostyka sprawdza polaczenie miedzy Twoja aplikacja a serwerem");
            tutorial.AppendLine("  SQL Server z systemem UNICARD RCP (rejestracja czasu pracy).");
            tutorial.AppendLine("  Serwer: 192.168.0.23\\SQLEXPRESS, baza: UNISYSTEM");
            tutorial.AppendLine();
            tutorial.AppendLine("  Ikona w raporcie:  PASS = test przeszedl pomyslnie");
            tutorial.AppendLine("                     FAIL = test nie przeszedl - wymaga naprawy");
            tutorial.AppendLine("                     SKIP/brak = test pominiety (nieistotny)");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  NAGLOWEK RAPORTU");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Pokazuje informacje o Twoim komputerze i ustawieniach polaczenia:");
            tutorial.AppendLine("  - Komputer / Uzytkownik   → nazwa Twojego PC i login Windows");
            tutorial.AppendLine("  - .NET Runtime            → wersja srodowiska (wymagane 8.0+)");
            tutorial.AppendLine("  - App UserID / UserName   → zalogowany uzytkownik w aplikacji");
            tutorial.AppendLine("  - Connection String       → dane polaczenia (serwer, baza, login)");
            tutorial.AppendLine("    UWAGA: Haslo jest ukryte gwiazdkami - to normalne.");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  TEST 1: PING SERWERA");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Sprawdza czy serwer 192.168.0.23 odpowiada w sieci.");
            tutorial.AppendLine();
            tutorial.AppendLine("  PASS → Serwer jest dostepny, wyswietla czas odpowiedzi (np. 2ms).");
            tutorial.AppendLine("         Dobry czas: <5ms (siec lokalna), <50ms (VPN).");
            tutorial.AppendLine();
            tutorial.AppendLine("  FAIL (TimedOut) → Serwer nie odpowiada. Mozliwe przyczyny:");
            tutorial.AppendLine("    1. Serwer jest wylaczony → wlacz go fizycznie");
            tutorial.AppendLine("    2. Kabel sieciowy odlaczony → sprawdz kabel / Wi-Fi na serwerze");
            tutorial.AppendLine("    3. Serwer zmienil IP → sprawdz IP: na serwerze cmd → ipconfig");
            tutorial.AppendLine("    4. Firewall blokuje ICMP → na serwerze wylacz blokade pinga:");
            tutorial.AppendLine("       netsh advfirewall firewall add rule name=\"Ping\"");
            tutorial.AppendLine("         dir=in action=allow protocol=icmpv4");
            tutorial.AppendLine("    5. Jestes w innej sieci (np. VPN) → polacz sie z siecia firmowa");
            tutorial.AppendLine();
            tutorial.AppendLine("  SKIP → Ping niedostepny - nieistotne, przejdz do testu 2.");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  TEST 2: PORT TCP 1433");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Sprawdza czy SQL Server nasluchuje na porcie 1433 (standardowy port SQL).");
            tutorial.AppendLine();
            tutorial.AppendLine("  PASS → Port otwarty, SQL Server przyjmuje polaczenia.");
            tutorial.AppendLine();
            tutorial.AppendLine("  FAIL (timeout) → Port zamkniety. Mozliwe przyczyny:");
            tutorial.AppendLine("    1. SQL Server nie nasluchuje na TCP/IP → na serwerze:");
            tutorial.AppendLine("       a) Otworz SQL Server Configuration Manager");
            tutorial.AppendLine("          (Win+R → SQLServerManager15.msc)");
            tutorial.AppendLine("       b) SQL Server Network Configuration → Protocols for SQLEXPRESS");
            tutorial.AppendLine("       c) TCP/IP → prawy klik → Enable");
            tutorial.AppendLine("       d) Dwuklik TCP/IP → IP Addresses → IPAll:");
            tutorial.AppendLine("          TCP Dynamic Ports = (puste)");
            tutorial.AppendLine("          TCP Port = 1433");
            tutorial.AppendLine("       e) Zrestartuj SQL Server:");
            tutorial.AppendLine("          net stop \"SQL Server (SQLEXPRESS)\"");
            tutorial.AppendLine("          net start \"SQL Server (SQLEXPRESS)\"");
            tutorial.AppendLine();
            tutorial.AppendLine("    2. Firewall blokuje port → na serwerze (cmd jako Administrator):");
            tutorial.AppendLine("       netsh advfirewall firewall add rule name=\"SQL Server 1433\"");
            tutorial.AppendLine("         dir=in action=allow protocol=tcp localport=1433");
            tutorial.AppendLine("       netsh advfirewall firewall add rule name=\"SQL Server Browser\"");
            tutorial.AppendLine("         dir=in action=allow protocol=udp localport=1434");
            tutorial.AppendLine();
            tutorial.AppendLine("    3. SQL Server Browser nie dziala (potrzebny dla \\SQLEXPRESS):");
            tutorial.AppendLine("       net start \"SQL Server Browser\"");
            tutorial.AppendLine();
            tutorial.AppendLine("  Po naprawie sprawdz na serwerze:");
            tutorial.AppendLine("    netstat -an | findstr 1433");
            tutorial.AppendLine("  Powinno pokazac: 0.0.0.0:1433  LISTENING");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  TEST 3: POLACZENIE SQL");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Probuje zalogowac sie do SQL Server uzywajac danych z Connection String.");
            tutorial.AppendLine();
            tutorial.AppendLine("  PASS → Polaczenie udane. Wyswietla:");
            tutorial.AppendLine("    - SQL Server wersja   → np. 15.0.2000.5 (SQL Server 2019)");
            tutorial.AppendLine("    - Edition             → Express, Standard, Enterprise");
            tutorial.AppendLine("    - SQL User             → kto jest zalogowany (sa)");
            tutorial.AppendLine("    Czas polaczenia <100ms = OK, >500ms = wolna siec");
            tutorial.AppendLine();
            tutorial.AppendLine("  FAIL → Blad logowania. Najczestsze bledy:");
            tutorial.AppendLine();
            tutorial.AppendLine("    Error 18456 (Login failed):");
            tutorial.AppendLine("      → Zle haslo lub login. Sprawdz haslo w SSMS.");
            tutorial.AppendLine("      → SQL Server moze nie miec wlaczonego 'SQL Server Authentication'");
            tutorial.AppendLine("        W SSMS: prawy klik na serwer → Properties → Security");
            tutorial.AppendLine("        → zaznacz 'SQL Server and Windows Authentication mode'");
            tutorial.AppendLine();
            tutorial.AppendLine("    Error 26 (Error Locating Server/Instance):");
            tutorial.AppendLine("      → SQL Server Browser nie dziala. Uruchom:");
            tutorial.AppendLine("        net start \"SQL Server Browser\"");
            tutorial.AppendLine("      → Lub zla nazwa instancji (nie SQLEXPRESS a inna).");
            tutorial.AppendLine("        Sprawdz w services.msc jaka instancja jest zainstalowana.");
            tutorial.AppendLine();
            tutorial.AppendLine("    Error -1 (network-related):");
            tutorial.AppendLine("      → Serwer niedostepny - wroc do testow 1 i 2.");
            tutorial.AppendLine();
            tutorial.AppendLine("  UWAGA: Jezeli test 3 FAIL, diagnostyka zostaje przerwana.");
            tutorial.AppendLine("  Napierw napraw polaczenie, potem uruchom diagnostyke ponownie.");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  TEST 4: WIDOKI I TABELE");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Sprawdza czy wymagane widoki/tabele istnieja w bazie UNISYSTEM.");
            tutorial.AppendLine("  Dla kazdego widoku wyswietla liste kolumn z typami danych.");
            tutorial.AppendLine();
            tutorial.AppendLine("  Wymagane widoki:");
            tutorial.AppendLine("  ┌──────────────────────────────────┬──────────────────────────────┐");
            tutorial.AppendLine("  │ Widok                            │ Do czego sluzy               │");
            tutorial.AppendLine("  ├──────────────────────────────────┼──────────────────────────────┤");
            tutorial.AppendLine("  │ V_RCINEG_EMPLOYEES_GROUPS        │ Dzialy / grupy pracownikow   │");
            tutorial.AppendLine("  │ V_RCINE_EMPLOYEES                │ Lista pracownikow            │");
            tutorial.AppendLine("  │ V_KDINAR_ALL_REGISTRATIONS       │ Rejestracje wejsc/wyjsc      │");
            tutorial.AppendLine("  │ V_KDINEC_EMPLOYEES_CARDS         │ Przypisane karty RCP         │");
            tutorial.AppendLine("  │ T_KDCAC_CARDS                    │ Tabela kart (fizyczne karty) │");
            tutorial.AppendLine("  └──────────────────────────────────┴──────────────────────────────┘");
            tutorial.AppendLine();
            tutorial.AppendLine("  PASS → Widok istnieje. Lista kolumn z przykladowymi wartosciami:");
            tutorial.AppendLine("    [ 0] RCINE_EMPLOYEE_ID    int     (.NET: Int32) = 1234");
            tutorial.AppendLine("    ─── numer ──────────────── typ SQL ── typ .NET ─── wartosc ──");
            tutorial.AppendLine();
            tutorial.AppendLine("  FAIL → Widok nie istnieje lub brak uprawnien.");
            tutorial.AppendLine("    → Sprawdz w SSMS czy widok istnieje w bazie UNISYSTEM.");
            tutorial.AppendLine("    → Moze trzeba przeinstalowac/zaktualizowac UNICARD.");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  TEST 5: LICZNOSC DANYCH");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Zlicza rekordy w kazdym widoku. Pomaga sprawdzic czy sa dane.");
            tutorial.AppendLine();
            tutorial.AppendLine("  Grupy (unikalne)          = ile jest dzialow (np. 15)");
            tutorial.AppendLine("  Pracownicy (typ=1)        = ile aktywnych pracownikow (np. 200)");
            tutorial.AppendLine("  Rejestracje (dzis)        = ile rejestacji dzisiaj (np. 350)");
            tutorial.AppendLine("  Rejestracje (wczoraj)     = ile rejestacji wczoraj");
            tutorial.AppendLine("  Rejestracje (ten miesiac) = ile rejestacji w tym miesiacu");
            tutorial.AppendLine("  Rejestracje (ogolem)      = ile rekordow w calej bazie");
            tutorial.AppendLine("  Karty (aktywne)            = ile kart jest aktualnie przypisanych");
            tutorial.AppendLine();
            tutorial.AppendLine("  Jesli 'Rejestracje (dzis) = 0' ale jest dzien roboczy:");
            tutorial.AppendLine("    → Czytniki RCP moga nie dzialac lub nie przesylac danych.");
            tutorial.AppendLine("    → Sprawdz w SSMS ostatnia rejestracje:");
            tutorial.AppendLine("      SELECT TOP 5 * FROM V_KDINAR_ALL_REGISTRATIONS");
            tutorial.AppendLine("      ORDER BY KDINAR_REGISTRTN_DATETIME DESC");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  TEST 6: PROBKA REJESTRACJI");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Wyswietla 5 ostatnich rejestracji z pelnym zrzutem kolumn.");
            tutorial.AppendLine("  Kazda kolumna pokazuje:");
            tutorial.AppendLine("    NAZWA_KOLUMNY    [typ_sql → typ_.NET]  = wartosc");
            tutorial.AppendLine();
            tutorial.AppendLine("  Wazne kolumny do sprawdzenia:");
            tutorial.AppendLine("  - KDINAR_REGISTRTN_TYPE → typ rejestracji.");
            tutorial.AppendLine("    Moze byc int (0/1) lub string ('WEJSCIE'/'WYJSCIE').");
            tutorial.AppendLine("    Aplikacja obsluguje oba formaty.");
            tutorial.AppendLine("  - KDINAR_REGISTRTN_DATETIME → data/godzina rejestracji.");
            tutorial.AppendLine("    Sprawdz czy daty sa aktualne (a nie np. sprzed roku).");
            tutorial.AppendLine("  - KDINAR_ACCESS_POINT_NAME → nazwa punktu dostepu (czytnika).");
            tutorial.AppendLine("    Jesli zawiera 'WE' = wejscie, 'WY' = wyjscie.");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  TEST 7: PROBKA PRACOWNIKOW");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Wyswietla 3 pierwszych pracownikow (typ=1 = aktywni).");
            tutorial.AppendLine("  Sprawdz czy imiona i nazwiska sie wyswietlaja poprawnie.");
            tutorial.AppendLine("  Jesli widac 'krzaczki' → problem z kodowaniem znakow (collation).");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  TEST 8: ZAPYTANIA APLIKACJI");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Testuje dokladnie te same zapytania SQL co aplikacja:");
            tutorial.AppendLine();
            tutorial.AppendLine("  [LoadGrupy]        → laduje dzialy do filtra 'Grupa/Dzial'");
            tutorial.AppendLine("  [LoadPracownicy]   → laduje liste pracownikow do comboboxow");
            tutorial.AppendLine("  [LoadAllData]       → laduje rejestracje wejsc/wyjsc (glowne dane)");
            tutorial.AppendLine();
            tutorial.AppendLine("  Jezeli ktorykolwiek FAIL → to jest dokladna przyczyna bledu w aplikacji!");
            tutorial.AppendLine("  Tresc bledu powie co jest nie tak (zla kolumna, brak uprawnien itp.).");
            tutorial.AppendLine();
            tutorial.AppendLine("  [Opcjonalne kolumny] → sprawdza dodatkowe kolumny w V_KDINAR:");
            tutorial.AppendLine("    - KDINAR_CARD_NUMBER    = numer karty RCP");
            tutorial.AppendLine("    - KDINAR_DEVICE_NAME     = nazwa urzadzenia (czytnika)");
            tutorial.AppendLine("    - KDINAR_REGISTRTN_MODE  = tryb rejestracji");
            tutorial.AppendLine("    Te kolumny NIE SA WYMAGANE - aplikacja dziala bez nich.");
            tutorial.AppendLine("    Jesli widac 'NIE ISTNIEJE' - to normalne, nie jest to blad.");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  TEST 9: UPRAWNIENIA SQL");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Sprawdza uprawnienia zalogowanego uzytkownika SQL (sa).");
            tutorial.AppendLine("  - sysadmin: TAK  → pelne uprawnienia (prawidlowe dla 'sa')");
            tutorial.AppendLine("  - sysadmin: NIE  → ograniczone uprawnienia");
            tutorial.AppendLine("    Moze powodowac problemy z odczytem niektorych widokow.");
            tutorial.AppendLine("    Rozwiazanie: w SSMS dodaj uzytkownika do roli sysadmin");
            tutorial.AppendLine("    lub nadaj uprawnienia SELECT do widokow UNICARD.");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  TEST 10: STATUS SERWERA");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Informacje o serwerze SQL:");
            tutorial.AppendLine("  - Aktywne sesje    → ile polaczen jest aktualnie (np. 15)");
            tutorial.AppendLine("  - SQL uruchomiony  → kiedy ostatnio restartowano SQL Server");
            tutorial.AppendLine("  - RAM serwera      → ile pamieci ma serwer");
            tutorial.AppendLine("  - Rozmiar bazy     → ile wazy baza UNISYSTEM w MB");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  STAN APLIKACJI (pamiec)");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Ile danych jest aktualnie zaladowanych w pamieci aplikacji:");
            tutorial.AppendLine("  - Grupy zaladowane       → powinno byc >0 (np. 15)");
            tutorial.AppendLine("  - Pracownicy zaladowani  → powinno byc >0 (np. 200)");
            tutorial.AppendLine("  - Rejestracje w pamieci  → zalezy od wybranego zakresu dat");
            tutorial.AppendLine("  Jesli wszystkie = 0, dane nie zostaly zaladowane (blad na starcie).");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  PODSUMOWANIE WYNIKOW");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  Na koncu raportu jest podsumowanie:");
            tutorial.AppendLine("    X PASS / Y FAIL");
            tutorial.AppendLine();
            tutorial.AppendLine("  WSZYSTKO OK     → wszystkie testy przeszly, system dziala prawidlowo.");
            tutorial.AppendLine("  WYKRYTO PROBLEMY → sa bledy - przejrzyj testy oznaczone FAIL.");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  NAJCZESTSZE PROBLEMY I ROZWIAZANIA");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  PROBLEM: Ping FAIL, Port FAIL, Polaczenie FAIL");
            tutorial.AppendLine("  ─────────────────────────────────────────────────");
            tutorial.AppendLine("  Serwer jest niedostepny w sieci.");
            tutorial.AppendLine("  → Sprawdz czy serwer jest wlaczony");
            tutorial.AppendLine("  → Sprawdz kabel sieciowy / Wi-Fi");
            tutorial.AppendLine("  → Sprawdz czy jestes w tej samej sieci (192.168.0.x)");
            tutorial.AppendLine();
            tutorial.AppendLine("  PROBLEM: Ping PASS, Port FAIL");
            tutorial.AppendLine("  ──────────────────────────────");
            tutorial.AppendLine("  Serwer dziala, ale SQL Server nie nasluchuje lub firewall blokuje.");
            tutorial.AppendLine("  → Na serwerze wlacz TCP/IP w SQL Server Configuration Manager");
            tutorial.AppendLine("  → Otworz port 1433 w firewallu (patrz TEST 2 powyzej)");
            tutorial.AppendLine("  → Uruchom SQL Server Browser");
            tutorial.AppendLine();
            tutorial.AppendLine("  PROBLEM: Ping PASS, Port PASS, Polaczenie FAIL (Error 18456)");
            tutorial.AppendLine("  ──────────────────────────────────────────────────────────────");
            tutorial.AppendLine("  Zle haslo lub login do SQL Server.");
            tutorial.AppendLine("  → Sprawdz haslo w SSMS (SQL Server Management Studio)");
            tutorial.AppendLine("  → Sprawdz czy SQL Authentication jest wlaczone");
            tutorial.AppendLine();
            tutorial.AppendLine("  PROBLEM: Polaczenie PASS, ale Widoki FAIL");
            tutorial.AppendLine("  ──────────────────────────────────────────");
            tutorial.AppendLine("  Baza dziala, ale brakuje widokow UNICARD.");
            tutorial.AppendLine("  → UNICARD moze nie byc zainstalowany na tej bazie");
            tutorial.AppendLine("  → Sprawdz nazwe bazy (UNISYSTEM) - moze byc inna");
            tutorial.AppendLine("  → Skontaktuj sie z dostawca UNICARD");
            tutorial.AppendLine();
            tutorial.AppendLine("  PROBLEM: Wszystko PASS, ale 'Rejestracje dzis = 0'");
            tutorial.AppendLine("  ─────────────────────────────────────────────────────");
            tutorial.AppendLine("  Baza dziala, ale czytniki RCP nie przesylaja danych.");
            tutorial.AppendLine("  → Sprawdz czy czytniki sa wlaczone i podlaczone do sieci");
            tutorial.AppendLine("  → Sprawdz serwis UNICARD na serwerze (uslugi)");
            tutorial.AppendLine("  → Sprawdz ostatnia date rejestracji w sekcji 'Probka rejestracji'");
            tutorial.AppendLine();
            tutorial.AppendLine();
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine("  JAK UZYSKAC POMOC");
            tutorial.AppendLine("══════════════════════════════════════════════════════════════════════════════");
            tutorial.AppendLine();
            tutorial.AppendLine("  1. Kliknij 'Kopiuj do schowka' w oknie diagnostyki");
            tutorial.AppendLine("  2. Wklej raport do maila lub komunikatora");
            tutorial.AppendLine("  3. Wyslij do administratora IT lub dostawcy UNICARD");
            tutorial.AppendLine("  4. Raport zawiera wszystkie potrzebne informacje do diagnozy");
            tutorial.AppendLine();
            tutorial.AppendLine("  Mozesz tez kliknac 'Zapisz do pliku' aby zachowac raport jako .txt");
            tutorial.AppendLine();

            var helpWindow = new Window
            {
                Title = "Jak korzystac z diagnostyki SQL",
                Width = 900,
                Height = 750,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(Color.FromRgb(26, 32, 44))
            };
            WindowIconHelper.SetIcon(helpWindow);

            var helpGrid = new Grid();
            helpGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            helpGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var helpText = new TextBox
            {
                Text = tutorial.ToString(),
                IsReadOnly = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Background = new SolidColorBrush(Color.FromRgb(26, 32, 44)),
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                TextWrapping = TextWrapping.NoWrap
            };
            Grid.SetRow(helpText, 0);
            helpGrid.Children.Add(helpText);

            var helpBtnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(16, 10, 16, 10)
            };
            Grid.SetRow(helpBtnPanel, 1);

            var btnCopyHelp = new Button
            {
                Content = "Kopiuj tutorial",
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 13,
                Background = new SolidColorBrush(Color.FromRgb(66, 153, 225)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCopyHelp.Click += (s2, ev2) =>
            {
                Clipboard.SetText(tutorial.ToString());
                btnCopyHelp.Content = "Skopiowano!";
                var t2 = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                t2.Tick += (t3, te3) => { btnCopyHelp.Content = "Kopiuj tutorial"; t2.Stop(); };
                t2.Start();
            };

            var btnCloseHelp = new Button
            {
                Content = "Zamknij",
                Padding = new Thickness(20, 8, 20, 8),
                FontSize = 13,
                Background = new SolidColorBrush(Color.FromRgb(113, 128, 150)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCloseHelp.Click += (s2, ev2) => helpWindow.Close();

            helpBtnPanel.Children.Add(btnCopyHelp);
            helpBtnPanel.Children.Add(btnCloseHelp);
            helpGrid.Children.Add(helpBtnPanel);

            helpWindow.Content = helpGrid;
            helpWindow.ShowDialog();
        }

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
