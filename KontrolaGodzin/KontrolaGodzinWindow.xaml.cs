using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
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

                // Ładowanie danych
                LoadGrupy();
                LoadPracownicy();
                LoadAllData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd inicjalizacji: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show($"Błąd ładowania grup: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                
                // Fallback - pusta lista
                cmbGrupa.ItemsSource = new[] { new GrupaModel { Id = 0, Nazwa = "-- Wszystkie działy --" } };
                cmbGrupa.SelectedIndex = 0;
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

                cmbPracownikEwidencja.ItemsSource = _pracownicy;
                cmbPracownikEwidencja.DisplayMemberPath = "PelneNazwisko";
                cmbPracownikEwidencja.SelectedValuePath = "Id";
                cmbPracownikEwidencja.SelectedIndex = 0;

                cmbHistoriaPracownik.ItemsSource = _pracownicy;
                cmbHistoriaPracownik.DisplayMemberPath = "PelneNazwisko";
                cmbHistoriaPracownik.SelectedValuePath = "Id";
                cmbHistoriaPracownik.SelectedIndex = 0;

                // Załaduj listę agencji
                LoadAgencje();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania pracowników: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                        Status = status
                    };
                })
                .OrderByDescending(x => x.Data)
                .ThenBy(x => x.Pracownik)
                .ToList();

            gridGodzinyPracy.ItemsSource = grouped;
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

            gridAlerty.ItemsSource = alerty.OrderByDescending(a => ((dynamic)a).Priorytet).ToList();
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

            gridNadgodziny.ItemsSource = nadgodziny;
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
            LoadGrupy();
            LoadPracownicy();
            LoadAllData();
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
        private void BtnGenerujRaportMiesieczny_Click(object sender, RoutedEventArgs e) { }
        private void BtnDrukujRaport_Click(object sender, RoutedEventArgs e) { }
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

    #endregion
}
