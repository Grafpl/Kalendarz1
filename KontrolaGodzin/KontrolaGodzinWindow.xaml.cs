using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Kalendarz1.KontrolaGodzin
{
    public partial class KontrolaGodzinWindow : Window
    {
        // Connection string do UNICARD
        private const string connectionUnicard = "Server=192.168.0.23\\SQLEXPRESS;Database=UNISYSTEM;User Id=sa;Password=UniRCPAdmin123$;TrustServerCertificate=True";

        private ObservableCollection<RejestrKarty> rejestracje = new ObservableCollection<RejestrKarty>();
        private ObservableCollection<DzienPracy> godzinyPracy = new ObservableCollection<DzienPracy>();
        private ObservableCollection<StatusObecnosci> obecni = new ObservableCollection<StatusObecnosci>();
        private ObservableCollection<PodsumowanieAgencji> podsumowania = new ObservableCollection<PodsumowanieAgencji>();
        private ObservableCollection<AlertCzasuPracy> alerty = new ObservableCollection<AlertCzasuPracy>();
        private List<GrupaUnicard> grupy = new List<GrupaUnicard>();

        private const string SQL_REJESTRACJE = @"
            WITH RejestracjeBezDuplikatow AS (
                SELECT 
                    KDINAR_REGISTRTN_ID,
                    KDINAR_REGISTRTN_DATETIME,
                    CASE KDINAR_REGISTRTN_TYPE WHEN 1 THEN 'Wejście' WHEN 2 THEN 'Wyjście' ELSE 'Nieznany' END AS Typ,
                    KDINAR_EMPLOYEE_ID AS IdPracownika,
                    KDINAR_EMPLOYEE_NAME AS Imie,
                    KDINAR_EMPLOYEE_SURNAME AS Nazwisko,
                    KDINAR_EMPLOYEE_GROUP_ID AS IdGrupy,
                    KDINAR_ACCESS_POINT_NAME AS PunktDostepu,
                    CASE 
                        WHEN KDINAR_ACCESS_POINT_NAME LIKE '%Portiernia%' THEN 'Portiernia'
                        WHEN KDINAR_ACCESS_POINT_NAME LIKE '%Produkcja%' THEN 'Produkcja'
                        ELSE 'Inny'
                    END AS TypPunktu,
                    ROW_NUMBER() OVER (
                        PARTITION BY KDINAR_EMPLOYEE_ID, 
                                     CONVERT(DATE, KDINAR_REGISTRTN_DATETIME),
                                     DATEPART(HOUR, KDINAR_REGISTRTN_DATETIME),
                                     DATEPART(MINUTE, KDINAR_REGISTRTN_DATETIME),
                                     KDINAR_REGISTRTN_TYPE
                        ORDER BY KDINAR_REGISTRTN_ID
                    ) AS rn
                FROM V_KDINAR_ALL_REGISTRATIONS
                WHERE KDINAR_REGISTRTN_DATETIME >= @odDaty 
                  AND KDINAR_REGISTRTN_DATETIME < @doDaty
                  AND KDINAR_EMPLOYEE_ID IS NOT NULL
            )
            SELECT * FROM RejestracjeBezDuplikatow WHERE rn = 1
            ORDER BY KDINAR_REGISTRTN_DATETIME DESC";

        private const string SQL_OBECNI = @"
            WITH OstatnieZdarzenia AS (
                SELECT 
                    KDINAR_EMPLOYEE_ID,
                    KDINAR_EMPLOYEE_NAME,
                    KDINAR_EMPLOYEE_SURNAME,
                    KDINAR_EMPLOYEE_GROUP_ID,
                    KDINAR_REGISTRTN_DATETIME,
                    KDINAR_REGISTRTN_TYPE,
                    KDINAR_ACCESS_POINT_NAME,
                    CASE 
                        WHEN KDINAR_ACCESS_POINT_NAME LIKE '%Portiernia%' THEN 'Portiernia'
                        WHEN KDINAR_ACCESS_POINT_NAME LIKE '%Produkcja%' THEN 'Produkcja'
                        ELSE 'Inny'
                    END AS TypPunktu,
                    ROW_NUMBER() OVER (PARTITION BY KDINAR_EMPLOYEE_ID ORDER BY KDINAR_REGISTRTN_DATETIME DESC) AS rn
                FROM V_KDINAR_ALL_REGISTRATIONS
                WHERE CONVERT(DATE, KDINAR_REGISTRTN_DATETIME) = CONVERT(DATE, GETDATE())
                  AND KDINAR_EMPLOYEE_ID IS NOT NULL
            )
            SELECT * FROM OstatnieZdarzenia
            WHERE rn = 1 AND KDINAR_REGISTRTN_TYPE = 1
            ORDER BY KDINAR_EMPLOYEE_SURNAME, KDINAR_EMPLOYEE_NAME";

        private readonly Dictionary<int, string> nazwyGrup = new Dictionary<int, string>
        {
            { 10003, "CZYSTA" }, { 10004, "BIURO" }, { 10005, "SPRZEDAWCA" },
            { 10006, "KIEROWCA" }, { 10007, "MAGAZYN" }, { 10008, "MASARNIA" },
            { 10009, "MROZNIA" }, { 10010, "MECHANIK" }, { 10012, "ODPADY" },
            { 10014, "MYJKA" }, { 10015, "Avilog" }, { 10016, "SPRZATACZKA" },
            { 10017, "PORTIERZY" }, { 10019, "OCHRONA" }, { 10020, "AGENCJA GURAVO" },
            { 10023, "TYMCZASOWI" }, { 10024, "AGENCJA STAR-POL" },
            { 10027, "AGENCJA IMPULS" }, { 10028, "AGENCJA ROB-JOB" },
            { 10029, "AGENCJA ECO-MEN" }, { 10030, "AGENCJA (Moldawianie)" }
        };

        private DispatcherTimer clockTimer;
        private bool isInitialized = false;
        private bool isLoading = false;

        public KontrolaGodzinWindow()
        {
            InitializeComponent();
            gridRejestracje.ItemsSource = rejestracje;
            gridGodzinyPracy.ItemsSource = godzinyPracy;
            gridObecni.ItemsSource = obecni;
            gridPodsumowanie.ItemsSource = podsumowania;
            gridAlerty.ItemsSource = alerty;

            clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clockTimer.Tick += (s, e) =>
            {
                txtAktualnaData.Text = DateTime.Now.ToString("dddd, d MMMM yyyy");
                txtAktualnaGodzina.Text = DateTime.Now.ToString("HH:mm:ss");
            };
            clockTimer.Start();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            isLoading = true;
            dpOd.SelectedDate = DateTime.Today;
            dpDo.SelectedDate = DateTime.Today;
            dpOd.IsEnabled = false;
            dpDo.IsEnabled = false;
            LoadGrupy();
            isLoading = false;
            isInitialized = true;
            await LoadAllData();
        }

        private async void ChkTylkoDzisiaj_Changed(object sender, RoutedEventArgs e)
        {
            if (!isInitialized) return;
            
            if (chkTylkoDzisiaj.IsChecked == true)
            {
                isLoading = true;
                dpOd.SelectedDate = DateTime.Today;
                dpDo.SelectedDate = DateTime.Today;
                dpOd.IsEnabled = false;
                dpDo.IsEnabled = false;
                isLoading = false;
                await LoadAllData();
            }
            else
            {
                dpOd.IsEnabled = true;
                dpDo.IsEnabled = true;
            }
        }

        private async void DpOd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInitialized || isLoading || chkTylkoDzisiaj.IsChecked == true) return;
            if (!dpOd.SelectedDate.HasValue || !dpDo.SelectedDate.HasValue) return;
            
            // Jeśli data Od > Do, ustaw Do = Od
            if (dpOd.SelectedDate > dpDo.SelectedDate)
                dpDo.SelectedDate = dpOd.SelectedDate;
            else
                await LoadAllData();
        }

        private async void DpDo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInitialized || isLoading || chkTylkoDzisiaj.IsChecked == true) return;
            if (!dpOd.SelectedDate.HasValue || !dpDo.SelectedDate.HasValue) return;
            
            // Jeśli data Do < Od, ustaw Od = Do
            if (dpDo.SelectedDate < dpOd.SelectedDate)
                dpOd.SelectedDate = dpDo.SelectedDate;
            else
                await LoadAllData();
        }

        private void CmbGrupa_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) => await LoadAllData();
        private void BtnExport_Click(object sender, RoutedEventArgs e) => ExportToExcel();
        
        private void BtnWyczyscSzukaj_Click(object sender, RoutedEventArgs e)
        {
            txtSzukaj.Text = "";
            cmbGrupa.SelectedIndex = 0;
        }

        private async void BtnWczoraj_Click(object sender, RoutedEventArgs e)
        {
            isLoading = true;
            chkTylkoDzisiaj.IsChecked = false;
            dpOd.SelectedDate = DateTime.Today.AddDays(-1);
            dpDo.SelectedDate = DateTime.Today.AddDays(-1);
            isLoading = false;
            await LoadAllData();
        }

        private async void BtnTydzien_Click(object sender, RoutedEventArgs e)
        {
            isLoading = true;
            chkTylkoDzisiaj.IsChecked = false;
            // Początek tygodnia (poniedziałek)
            var dzisiaj = DateTime.Today;
            var poczatekTygodnia = dzisiaj.AddDays(-(int)dzisiaj.DayOfWeek + (int)DayOfWeek.Monday);
            if (dzisiaj.DayOfWeek == DayOfWeek.Sunday) poczatekTygodnia = poczatekTygodnia.AddDays(-7);
            dpOd.SelectedDate = poczatekTygodnia;
            dpDo.SelectedDate = dzisiaj;
            isLoading = false;
            await LoadAllData();
        }

        private async void BtnMiesiac_Click(object sender, RoutedEventArgs e)
        {
            isLoading = true;
            chkTylkoDzisiaj.IsChecked = false;
            var dzisiaj = DateTime.Today;
            dpOd.SelectedDate = new DateTime(dzisiaj.Year, dzisiaj.Month, 1);
            dpDo.SelectedDate = dzisiaj;
            isLoading = false;
            await LoadAllData();
        }

        private void LoadGrupy()
        {
            cmbGrupa.Items.Clear();
            cmbGrupa.Items.Add(new GrupaUnicard { IdGrupy = 0, NazwaGrupy = "-- Wszystkie grupy --" });
            foreach (var g in nazwyGrup)
            {
                var grupa = new GrupaUnicard { IdGrupy = g.Key, NazwaGrupy = g.Value };
                grupy.Add(grupa);
                cmbGrupa.Items.Add(grupa);
            }
            cmbGrupa.SelectedIndex = 0;
        }

        private async Task LoadAllData()
        {
            if (isLoading) return;
            isLoading = true;
            
            btnOdswiez.IsEnabled = false;
            btnOdswiez.Content = "Ładowanie...";
            panelLadowanie.Visibility = Visibility.Visible;

            try
            {
                await Task.WhenAll(LoadRejestracje(), LoadObecni());
                ObliczGodzinyPracy();
                GenerujPodsumowanie();
                GenerujAlerty();
                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnOdswiez.IsEnabled = true;
                btnOdswiez.Content = "Odśwież";
                panelLadowanie.Visibility = Visibility.Collapsed;
                isLoading = false;
            }
        }

        private async Task LoadRejestracje()
        {
            var lista = new List<RejestrKarty>();
            try
            {
                using (var conn = new SqlConnection(connectionUnicard))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(SQL_REJESTRACJE, conn))
                    {
                        cmd.Parameters.AddWithValue("@odDaty", dpOd.SelectedDate ?? DateTime.Today);
                        // Data Do włącznie - dodajemy 1 dzień aby objąć cały dzień "Do"
                        cmd.Parameters.AddWithValue("@doDaty", (dpDo.SelectedDate ?? DateTime.Today).AddDays(1));
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var idGrupy = reader["IdGrupy"] != DBNull.Value ? Convert.ToInt32(reader["IdGrupy"]) : 0;
                                var nazwaGrupy = nazwyGrup.ContainsKey(idGrupy) ? nazwyGrup[idGrupy] : $"Grupa {idGrupy}";
                                var imie = reader["Imie"]?.ToString() ?? "";
                                var nazwisko = reader["Nazwisko"]?.ToString() ?? "";
                                var dataCzas = (DateTime)reader["KDINAR_REGISTRTN_DATETIME"];

                                lista.Add(new RejestrKarty
                                {
                                    Id = Convert.ToInt32(reader["KDINAR_REGISTRTN_ID"]),
                                    DataCzas = dataCzas,
                                    Godzina = dataCzas.ToString("HH:mm:ss"),
                                    Typ = reader["Typ"].ToString(),
                                    IdPracownika = Convert.ToInt32(reader["IdPracownika"]),
                                    Pracownik = $"{imie} {nazwisko}".Trim(),
                                    IdGrupy = idGrupy,
                                    Grupa = nazwaGrupy,
                                    PunktDostepu = reader["PunktDostepu"]?.ToString() ?? "",
                                    TypPunktu = reader["TypPunktu"]?.ToString() ?? "Inny"
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania rejestracji:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await Dispatcher.InvokeAsync(() =>
            {
                rejestracje.Clear();
                foreach (var r in lista) rejestracje.Add(r);
            });
        }

        private async Task LoadObecni()
        {
            var lista = new List<StatusObecnosci>();
            try
            {
                using (var conn = new SqlConnection(connectionUnicard))
                {
                    await conn.OpenAsync();
                    using (var cmd = new SqlCommand(SQL_OBECNI, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var idGrupy = reader["KDINAR_EMPLOYEE_GROUP_ID"] != DBNull.Value ? Convert.ToInt32(reader["KDINAR_EMPLOYEE_GROUP_ID"]) : 0;
                                var nazwaGrupy = nazwyGrup.ContainsKey(idGrupy) ? nazwyGrup[idGrupy] : $"Grupa {idGrupy}";
                                var imie = reader["KDINAR_EMPLOYEE_NAME"]?.ToString() ?? "";
                                var nazwisko = reader["KDINAR_EMPLOYEE_SURNAME"]?.ToString() ?? "";
                                var ostatnie = (DateTime)reader["KDINAR_REGISTRTN_DATETIME"];
                                var czasNaTerenie = DateTime.Now - ostatnie;

                                lista.Add(new StatusObecnosci
                                {
                                    IdPracownika = Convert.ToInt32(reader["KDINAR_EMPLOYEE_ID"]),
                                    Pracownik = $"{imie} {nazwisko}".Trim(),
                                    IdGrupy = idGrupy,
                                    Grupa = nazwaGrupy,
                                    Godzina = ostatnie.ToString("HH:mm"),
                                    CzasNaTerenie = $"{(int)czasNaTerenie.TotalHours}h {czasNaTerenie.Minutes}m",
                                    PunktDostepu = reader["KDINAR_ACCESS_POINT_NAME"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania obecnych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            await Dispatcher.InvokeAsync(() =>
            {
                obecni.Clear();
                foreach (var o in lista) obecni.Add(o);
            });
        }

        private void ObliczGodzinyPracy()
        {
            godzinyPracy.Clear();
            if (rejestracje.Count == 0) return;

            var grupowane = rejestracje.GroupBy(r => new { r.IdPracownika, Data = r.DataCzas.Date });
            foreach (var grupa in grupowane)
            {
                var rejestracjeGrupy = grupa.OrderBy(r => r.DataCzas).ToList();
                var pierwsza = rejestracjeGrupy.First();
                
                // Wszystkie wejścia i wyjścia
                var wszystkieWejscia = rejestracjeGrupy.Where(r => r.Typ == "Wejście").OrderBy(r => r.DataCzas).ToList();
                var wszystkieWyjscia = rejestracjeGrupy.Where(r => r.Typ == "Wyjście").OrderBy(r => r.DataCzas).ToList();

                // Dla godzin pracy - pierwsze wejście i ostatnie wyjście (przez Portiernię jeśli dostępne)
                var wejsciaPortiernia = wszystkieWejscia.Where(r => r.TypPunktu == "Portiernia").ToList();
                var wyjsciaPortiernia = wszystkieWyjscia.Where(r => r.TypPunktu == "Portiernia").ToList();
                
                var wejsciaDoPracy = wejsciaPortiernia.Any() ? wejsciaPortiernia : wszystkieWejscia;
                var wyjsciaDoPracy = wyjsciaPortiernia.Any() ? wyjsciaPortiernia : wszystkieWyjscia;

                var pierwszeWejscie = wejsciaDoPracy.Any() ? wejsciaDoPracy.First().DataCzas : (DateTime?)null;
                var ostatnieWyjscie = wyjsciaDoPracy.Any() ? wyjsciaDoPracy.Last().DataCzas : (DateTime?)null;

                // Oblicz przerwy - szukamy par: Wyjście -> Wejście (między pierwszym wejściem a ostatnim wyjściem)
                int liczbaPrzerw = 0;
                TimeSpan sumaPrzerw = TimeSpan.Zero;

                if (pierwszeWejscie.HasValue && ostatnieWyjscie.HasValue)
                {
                    // Posortuj wszystkie zdarzenia chronologicznie
                    var zdarzeniaWDniu = rejestracjeGrupy
                        .Where(r => r.DataCzas >= pierwszeWejscie.Value && r.DataCzas <= ostatnieWyjscie.Value)
                        .OrderBy(r => r.DataCzas)
                        .ToList();

                    // Znajdź przerwy: Wyjście -> następne Wejście
                    for (int i = 0; i < zdarzeniaWDniu.Count - 1; i++)
                    {
                        if (zdarzeniaWDniu[i].Typ == "Wyjście" && zdarzeniaWDniu[i + 1].Typ == "Wejście")
                        {
                            var czasPrzerwy = zdarzeniaWDniu[i + 1].DataCzas - zdarzeniaWDniu[i].DataCzas;
                            // Liczymy tylko przerwy krótsze niż 4 godziny (dłuższe to pewnie błąd danych)
                            if (czasPrzerwy.TotalHours < 4)
                            {
                                liczbaPrzerw++;
                                sumaPrzerw += czasPrzerwy;
                            }
                        }
                    }
                }

                TimeSpan? czasPracy = null;
                TimeSpan? czasEfektywny = null;
                string status = "OK";

                if (pierwszeWejscie.HasValue && ostatnieWyjscie.HasValue && ostatnieWyjscie > pierwszeWejscie)
                {
                    czasPracy = ostatnieWyjscie.Value - pierwszeWejscie.Value;
                    czasEfektywny = czasPracy.Value - sumaPrzerw;
                    
                    if (czasPracy.Value.TotalHours > 14) status = "Ponad 14h";
                    else if (sumaPrzerw.TotalHours > 2) status = "Długie przerwy";
                }
                else if (!pierwszeWejscie.HasValue) status = "Brak wejścia";
                else if (!ostatnieWyjscie.HasValue) status = "Brak wyjścia";
                else status = "Błąd danych";

                godzinyPracy.Add(new DzienPracy
                {
                    IdPracownika = grupa.Key.IdPracownika,
                    Pracownik = pierwsza.Pracownik,
                    IdGrupy = pierwsza.IdGrupy,
                    Grupa = pierwsza.Grupa,
                    Data = grupa.Key.Data,
                    PierwszeWejscie = pierwszeWejscie?.ToString("HH:mm") ?? "-",
                    OstatnieWyjscie = ostatnieWyjscie?.ToString("HH:mm") ?? "-",
                    CzasPracy = czasPracy.HasValue ? $"{(int)czasPracy.Value.TotalHours}h {czasPracy.Value.Minutes}m" : "-",
                    GodzinyDziesietne = czasPracy.HasValue ? (decimal)czasPracy.Value.TotalHours : 0,
                    LiczbaWejsc = wszystkieWejscia.Count,
                    LiczbaWyjsc = wszystkieWyjscia.Count,
                    LiczbaPrzerw = liczbaPrzerw,
                    CzasPrzerw = liczbaPrzerw > 0 ? $"{(int)sumaPrzerw.TotalHours}h {sumaPrzerw.Minutes}m" : "-",
                    GodzinyPrzerw = (decimal)sumaPrzerw.TotalHours,
                    CzasEfektywny = czasEfektywny.HasValue ? $"{(int)czasEfektywny.Value.TotalHours}h {czasEfektywny.Value.Minutes}m" : "-",
                    GodzinyEfektywne = czasEfektywny.HasValue ? (decimal)czasEfektywny.Value.TotalHours : 0,
                    Status = status
                });
            }
        }

        private void GenerujPodsumowanie()
        {
            podsumowania.Clear();
            if (godzinyPracy.Count == 0) return;

            foreach (var grupa in godzinyPracy.GroupBy(r => r.Grupa))
            {
                var pracownicy = grupa.Select(r => r.IdPracownika).Distinct().Count();
                var sumaGodzin = grupa.Sum(r => r.GodzinyEfektywne);
                var sumaPrzerw = grupa.Sum(r => r.GodzinyPrzerw);

                podsumowania.Add(new PodsumowanieAgencji
                {
                    Grupa = grupa.Key,
                    LiczbaPracownikow = pracownicy,
                    SumaGodzin = Math.Round(sumaGodzin, 1),
                    SredniaGodzin = pracownicy > 0 ? Math.Round(sumaGodzin / pracownicy, 1) : 0,
                    SumaPrzerw = Math.Round(sumaPrzerw, 1),
                    Obecni = obecni.Count(r => r.Grupa == grupa.Key),
                    BrakiWyjsc = grupa.Count(r => r.Status.Contains("Brak wyjścia")),
                    Przekroczenia = grupa.Count(r => r.Status.Contains("14h") || r.Status.Contains("przerwy"))
                });
            }
        }

        private void GenerujAlerty()
        {
            alerty.Clear();
            foreach (var dzien in godzinyPracy.Where(d => d.Status != "OK"))
            {
                string typ = dzien.Status.Contains("Brak wejścia") ? "Brak wejścia" :
                             dzien.Status.Contains("Brak wyjścia") ? "Brak wyjścia" :
                             dzien.Status.Contains("14h") ? "Przekroczenie czasu" :
                             dzien.Status.Contains("przerwy") ? "Długie przerwy" : "Inny problem";
                string priorytet = dzien.Status.Contains("Brak wejścia") ? "Wysoki" :
                                   dzien.Status.Contains("Brak wyjścia") ? "Średni" :
                                   dzien.Status.Contains("przerwy") ? "Niski" : "Niski";
                string opis = dzien.Status;
                if (dzien.LiczbaPrzerw > 0)
                {
                    opis += $" | {dzien.LiczbaPrzerw} przerw ({dzien.CzasPrzerw})";
                }

                alerty.Add(new AlertCzasuPracy
                {
                    Typ = typ, Priorytet = priorytet, Pracownik = dzien.Pracownik,
                    Grupa = dzien.Grupa, Data = dzien.Data, Opis = opis
                });
            }
        }

        private void UpdateStats()
        {
            txtStatRejestracje.Text = rejestracje.Count.ToString();
            txtStatRejestracjeFiltr.Text = "";
            txtStatObecni.Text = obecni.Count.ToString();
            
            // Pokaż godziny efektywne (bez przerw)
            var sumaEfektywna = godzinyPracy.Sum(g => g.GodzinyEfektywne);
            txtStatGodziny.Text = $"{Math.Round(sumaEfektywna, 1)}h";
            
            txtStatPracownicy.Text = godzinyPracy.Select(g => g.IdPracownika).Distinct().Count().ToString();
            txtStatAlerty.Text = alerty.Count.ToString();
            UpdateDateRangeDisplay();
            
            // Zastosuj filtry po załadowaniu danych
            ApplyFilters();
        }

        private void UpdateDateRangeDisplay()
        {
            if (dpOd.SelectedDate.HasValue && dpDo.SelectedDate.HasValue)
            {
                var od = dpOd.SelectedDate.Value;
                var doo = dpDo.SelectedDate.Value;
                
                if (od.Date == DateTime.Today && doo.Date == DateTime.Today)
                {
                    txtZakresDat.Text = "Dane z: dziś";
                }
                else if (od.Date == doo.Date)
                {
                    txtZakresDat.Text = $"Dane z: {od:dd.MM.yyyy}";
                }
                else
                {
                    txtZakresDat.Text = $"Dane z: {od:dd.MM.yyyy} - {doo:dd.MM.yyyy}";
                }
            }
        }

        private void ApplyFilters()
        {
            var wybranaGrupa = cmbGrupa.SelectedItem as GrupaUnicard;
            var szukaj = txtSzukaj.Text?.ToLower() ?? "";

            var filteredRejestracje = rejestracje.Where(r =>
                (wybranaGrupa == null || wybranaGrupa.IdGrupy == 0 || r.IdGrupy == wybranaGrupa.IdGrupy) &&
                (string.IsNullOrEmpty(szukaj) || r.Pracownik.ToLower().Contains(szukaj))).ToList();
            gridRejestracje.ItemsSource = filteredRejestracje;

            var filteredGodziny = godzinyPracy.Where(g =>
                (wybranaGrupa == null || wybranaGrupa.IdGrupy == 0 || g.IdGrupy == wybranaGrupa.IdGrupy) &&
                (string.IsNullOrEmpty(szukaj) || g.Pracownik.ToLower().Contains(szukaj))).ToList();
            gridGodzinyPracy.ItemsSource = filteredGodziny;

            var filteredObecni = obecni.Where(o =>
                (wybranaGrupa == null || wybranaGrupa.IdGrupy == 0 || o.IdGrupy == wybranaGrupa.IdGrupy) &&
                (string.IsNullOrEmpty(szukaj) || o.Pracownik.ToLower().Contains(szukaj))).ToList();
            gridObecni.ItemsSource = filteredObecni;

            // Pokaż info o filtrze
            bool maFiltr = !string.IsNullOrEmpty(szukaj) || (wybranaGrupa != null && wybranaGrupa.IdGrupy != 0);
            if (maFiltr)
            {
                txtStatRejestracjeFiltr.Text = $"(z {rejestracje.Count})";
                txtStatRejestracje.Text = filteredRejestracje.Count.ToString();
            }
            else
            {
                txtStatRejestracjeFiltr.Text = "";
                txtStatRejestracje.Text = rejestracje.Count.ToString();
            }
        }

        private void ExportToExcel()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Plik CSV|*.csv", FileName = $"KontrolaGodzin_{DateTime.Now:yyyy-MM-dd_HHmm}.csv"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        switch (tabControl.SelectedIndex)
                        {
                            case 0:
                                writer.WriteLine("Data;Godzina;Typ;Pracownik;Grupa;Punkt dostępu;Rodzaj");
                                foreach (var r in rejestracje) writer.WriteLine($"{r.DataCzas:dd.MM.yyyy};{r.Godzina};{r.Typ};{r.Pracownik};{r.Grupa};{r.PunktDostepu};{r.TypPunktu}");
                                break;
                            case 1:
                                writer.WriteLine("Pracownik;Grupa;Data;Wejście;Wyjście;Na miejscu;Godziny;Przerwy;Czas przerw;Godz. przerw;Efektywnie;Godz. efekt.;Wejść;Wyjść;Status");
                                foreach (var g in godzinyPracy) writer.WriteLine($"{g.Pracownik};{g.Grupa};{g.Data:dd.MM.yyyy};{g.PierwszeWejscie};{g.OstatnieWyjscie};{g.CzasPracy};{g.GodzinyDziesietne:N2};{g.LiczbaPrzerw};{g.CzasPrzerw};{g.GodzinyPrzerw:N2};{g.CzasEfektywny};{g.GodzinyEfektywne:N2};{g.LiczbaWejsc};{g.LiczbaWyjsc};{g.Status}");
                                break;
                            case 2:
                                writer.WriteLine("Pracownik;Grupa;Wejście;Na terenie;Punkt dostępu");
                                foreach (var o in obecni) writer.WriteLine($"{o.Pracownik};{o.Grupa};{o.Godzina};{o.CzasNaTerenie};{o.PunktDostepu}");
                                break;
                            case 3:
                                writer.WriteLine("Grupa;Pracownicy;Godz. efektywne;Średnia/os.;Godz. przerw;Obecni;Braki wyjść;Problemy");
                                foreach (var p in podsumowania) writer.WriteLine($"{p.Grupa};{p.LiczbaPracownikow};{p.SumaGodzin:N1};{p.SredniaGodzin:N1};{p.SumaPrzerw:N1};{p.Obecni};{p.BrakiWyjsc};{p.Przekroczenia}");
                                break;
                            case 4:
                                writer.WriteLine("Typ;Priorytet;Pracownik;Grupa;Data;Opis");
                                foreach (var a in alerty) writer.WriteLine($"{a.Typ};{a.Priorytet};{a.Pracownik};{a.Grupa};{a.Data:dd.MM.yyyy};{a.Opis}");
                                break;
                        }
                    }
                    MessageBox.Show($"Wyeksportowano do:\n{dialog.FileName}", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Błąd eksportu:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        protected override void OnClosed(EventArgs e) { clockTimer?.Stop(); base.OnClosed(e); }
    }

    // KLASY MODELI
    public class RejestrKarty
    {
        public int Id { get; set; }
        public DateTime DataCzas { get; set; }
        public string Godzina { get; set; }
        public string Typ { get; set; }
        public int IdPracownika { get; set; }
        public string Pracownik { get; set; }
        public int IdGrupy { get; set; }
        public string Grupa { get; set; }
        public string PunktDostepu { get; set; }
        public string TypPunktu { get; set; } // Portiernia, Produkcja, Inny
    }

    public class DzienPracy
    {
        public int IdPracownika { get; set; }
        public string Pracownik { get; set; }
        public int IdGrupy { get; set; }
        public string Grupa { get; set; }
        public DateTime Data { get; set; }
        public string PierwszeWejscie { get; set; }
        public string OstatnieWyjscie { get; set; }
        public string CzasPracy { get; set; }
        public decimal GodzinyDziesietne { get; set; }
        public int LiczbaWejsc { get; set; }
        public int LiczbaWyjsc { get; set; }
        public int LiczbaPrzerw { get; set; }
        public string CzasPrzerw { get; set; }
        public decimal GodzinyPrzerw { get; set; }
        public string CzasEfektywny { get; set; }
        public decimal GodzinyEfektywne { get; set; }
        public string Status { get; set; }
    }

    public class StatusObecnosci
    {
        public int IdPracownika { get; set; }
        public string Pracownik { get; set; }
        public int IdGrupy { get; set; }
        public string Grupa { get; set; }
        public string Godzina { get; set; }
        public string CzasNaTerenie { get; set; }
        public string PunktDostepu { get; set; }
    }

    public class PodsumowanieAgencji
    {
        public string Grupa { get; set; }
        public int LiczbaPracownikow { get; set; }
        public decimal SumaGodzin { get; set; }
        public decimal SredniaGodzin { get; set; }
        public decimal SumaPrzerw { get; set; }
        public int Obecni { get; set; }
        public int BrakiWyjsc { get; set; }
        public int Przekroczenia { get; set; }
    }

    public class AlertCzasuPracy
    {
        public string Typ { get; set; }
        public string Priorytet { get; set; }
        public string Pracownik { get; set; }
        public string Grupa { get; set; }
        public DateTime Data { get; set; }
        public string Opis { get; set; }
    }

    public class GrupaUnicard
    {
        public int IdGrupy { get; set; }
        public string NazwaGrupy { get; set; }
        public override string ToString() => NazwaGrupy;
    }
}
