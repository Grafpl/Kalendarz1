using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace Kalendarz1.KontrolaGodzin
{
    public partial class KontrolaGodzinWindow : Window
    {
        private const string ConnectionString = @"Server=192.168.0.23\SQLEXPRESS;Database=UNISYSTEM;User Id=sa;Password=UniRCPAdmin123$;TrustServerCertificate=True;";
        private DispatcherTimer clockTimer;
        private bool isLoading = false;

        // Kolekcje danych
        private ObservableCollection<RejestrKarty> rejestracje = new ObservableCollection<RejestrKarty>();
        private ObservableCollection<DzienPracy> godzinyPracy = new ObservableCollection<DzienPracy>();
        private ObservableCollection<StatusObecnosci> obecni = new ObservableCollection<StatusObecnosci>();
        private ObservableCollection<PodsumowanieAgencji> podsumowania = new ObservableCollection<PodsumowanieAgencji>();
        private ObservableCollection<AlertCzasuPracy> alerty = new ObservableCollection<AlertCzasuPracy>();
        private ObservableCollection<RankingPracownika> ranking = new ObservableCollection<RankingPracownika>();
        private ObservableCollection<RaportMiesiecznyPracownika> raportMiesieczny = new ObservableCollection<RaportMiesiecznyPracownika>();
        private ObservableCollection<PorownanieAgencjiModel> porownanieAgencji = new ObservableCollection<PorownanieAgencjiModel>();
        private ObservableCollection<NadgodzinyPracownika> nadgodziny = new ObservableCollection<NadgodzinyPracownika>();
        private ObservableCollection<DzienEwidencji> kartaEwidencji = new ObservableCollection<DzienEwidencji>();
        private ObservableCollection<AnalizaPunktualnosci> analizaPunktualnosci = new ObservableCollection<AnalizaPunktualnosci>();
        private ObservableCollection<Nieobecnosc> nieobecnosci = new ObservableCollection<Nieobecnosc>();
        private List<GrupaUnicard> grupy = new List<GrupaUnicard>();

        private readonly List<DateTime> swietaPolskie = new List<DateTime>();
        private TimeSpan godzinaPoczatkuPracy = new TimeSpan(6, 0, 0);
        private TimeSpan godzinaKoncaPracy = new TimeSpan(14, 0, 0);

        public KontrolaGodzinWindow()
        {
            InitializeComponent();

            gridRejestracje.ItemsSource = rejestracje;
            gridGodzinyPracy.ItemsSource = godzinyPracy;
            gridObecni.ItemsSource = obecni;
            gridPodsumowanie.ItemsSource = podsumowania;
            gridAlerty.ItemsSource = alerty;
            gridRanking.ItemsSource = ranking;
            gridRaportMiesieczny.ItemsSource = raportMiesieczny;
            gridPorownanie.ItemsSource = porownanieAgencji;
            gridNadgodziny.ItemsSource = nadgodziny;
            gridKartaEwidencji.ItemsSource = kartaEwidencji;
            gridPunktualnosc.ItemsSource = analizaPunktualnosci;
            gridNieobecnosci.ItemsSource = nieobecnosci;

            InicjalizujSwieta();
            InicjalizujComboBoxMiesiace();

            clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clockTimer.Tick += (s, e) =>
            {
                txtAktualnaData.Text = DateTime.Now.ToString("dddd, d MMMM yyyy");
                txtAktualnaGodzina.Text = DateTime.Now.ToString("HH:mm:ss");
            };
            clockTimer.Start();
        }

        private void InicjalizujSwieta()
        {
            int[] lata = { DateTime.Now.Year, DateTime.Now.Year + 1 };
            foreach (var rok in lata)
            {
                swietaPolskie.Add(new DateTime(rok, 1, 1));
                swietaPolskie.Add(new DateTime(rok, 1, 6));
                swietaPolskie.Add(new DateTime(rok, 5, 1));
                swietaPolskie.Add(new DateTime(rok, 5, 3));
                swietaPolskie.Add(new DateTime(rok, 8, 15));
                swietaPolskie.Add(new DateTime(rok, 11, 1));
                swietaPolskie.Add(new DateTime(rok, 11, 11));
                swietaPolskie.Add(new DateTime(rok, 12, 25));
                swietaPolskie.Add(new DateTime(rok, 12, 26));
            }
        }

        private void InicjalizujComboBoxMiesiace()
        {
            var miesiace = new[] { "Styczeń", "Luty", "Marzec", "Kwiecień", "Maj", "Czerwiec",
                                   "Lipiec", "Sierpień", "Wrzesień", "Październik", "Listopad", "Grudzień" };

            for (int i = 0; i < 12; i++)
            {
                var item = new ComboBoxItem { Content = miesiace[i], Tag = i + 1 };
                cmbMiesiac.Items.Add(item);
                cmbMiesiacEwidencja.Items.Add(new ComboBoxItem { Content = miesiace[i], Tag = i + 1 });
            }
            cmbMiesiac.SelectedIndex = DateTime.Now.Month - 1;
            cmbMiesiacEwidencja.SelectedIndex = DateTime.Now.Month - 1;

            for (int r = DateTime.Now.Year; r >= DateTime.Now.Year - 2; r--)
            {
                cmbRok.Items.Add(r);
                cmbRokEwidencja.Items.Add(r);
            }
            cmbRok.SelectedIndex = 0;
            cmbRokEwidencja.SelectedIndex = 0;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            dpOd.SelectedDate = DateTime.Today;
            dpDo.SelectedDate = DateTime.Today;
            await LoadGrupy();
            await LoadAllData();
        }

        private async Task LoadGrupy()
        {
            try
            {
                grupy.Clear();
                grupy.Add(new GrupaUnicard { Id = 0, Nazwa = "— Wszystkie działy —" });

                using (var conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync();
                    string sql = @"SELECT DISTINCT ine_numer, ine_nazwa FROM V_RCINE_EMPLOYEES WHERE ine_nazwa IS NOT NULL ORDER BY ine_nazwa";
                    using (var cmd = new SqlCommand(sql, conn))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            grupy.Add(new GrupaUnicard
                            {
                                Id = reader.GetInt32(0),
                                Nazwa = reader.GetString(1)
                            });
                        }
                    }
                }

                cmbGrupa.ItemsSource = grupy;
                cmbGrupa.DisplayMemberPath = "Nazwa";
                cmbGrupa.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania grup:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadAllData()
        {
            if (isLoading) return;
            isLoading = true;

            try
            {
                var dataOd = dpOd.SelectedDate ?? DateTime.Today;
                var dataDo = dpDo.SelectedDate ?? DateTime.Today;

                rejestracje.Clear();
                godzinyPracy.Clear();
                obecni.Clear();
                podsumowania.Clear();
                alerty.Clear();

                using (var conn = new SqlConnection(ConnectionString))
                {
                    await conn.OpenAsync();

                    string sql = @"
                        SELECT 
                            kdi_czas,
                            CASE WHEN kdi_wejscie = 1 THEN 'Wejście' ELSE 'Wyjście' END as Typ,
                            kdi_id_pracownika,
                            kdi_nazwa_pracownika,
                            kdi_nazwa_grupy,
                            kdi_nazwa_dostepu,
                            kdi_id_karty
                        FROM V_KDINAR_ALL_REGISTRATIONS
                        WHERE CAST(kdi_czas AS DATE) >= @dataOd 
                          AND CAST(kdi_czas AS DATE) <= @dataDo
                        ORDER BY kdi_czas DESC";

                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@dataOd", dataOd.Date);
                        cmd.Parameters.AddWithValue("@dataDo", dataDo.Date);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            var tempList = new List<RejestrKarty>();
                            while (await reader.ReadAsync())
                            {
                                tempList.Add(new RejestrKarty
                                {
                                    DataCzas = reader.GetDateTime(0),
                                    Godzina = reader.GetDateTime(0).ToString("HH:mm:ss"),
                                    Typ = reader.GetString(1),
                                    IdPracownika = reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                                    Pracownik = reader.IsDBNull(3) ? "Nieznany" : reader.GetString(3),
                                    Grupa = reader.IsDBNull(4) ? "Brak" : reader.GetString(4),
                                    PunktDostepu = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    IdKarty = reader.IsDBNull(6) ? "" : reader.GetString(6)
                                });
                            }

                            var deduplicated = tempList
                                .GroupBy(r => new { r.IdPracownika, r.Typ, Minuta = new DateTime(r.DataCzas.Year, r.DataCzas.Month, r.DataCzas.Day, r.DataCzas.Hour, r.DataCzas.Minute, 0) })
                                .Select(g => g.First())
                                .OrderByDescending(r => r.DataCzas);

                            foreach (var r in deduplicated)
                            {
                                r.TypPunktu = OkreslTypPunktu(r.PunktDostepu);
                                rejestracje.Add(r);
                            }
                        }
                    }
                }

                ObliczGodzinyPracy();
                ObliczObecnych();
                ObliczPodsumowania();
                GenerujAlerty();
                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd ładowania danych:\n{ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isLoading = false;
            }
        }

        private string OkreslTypPunktu(string nazwa)
        {
            if (string.IsNullOrEmpty(nazwa)) return "Inny";
            var lower = nazwa.ToLower();
            if (lower.Contains("portier") || lower.Contains("brama") || lower.Contains("wejście") || lower.Contains("główn"))
                return "Portiernia";
            if (lower.Contains("produk") || lower.Contains("hala") || lower.Contains("wydział"))
                return "Produkcja";
            return "Inny";
        }

        private void ObliczGodzinyPracy()
        {
            var grupowane = rejestracje
                .GroupBy(r => new { r.IdPracownika, Data = r.DataCzas.Date })
                .OrderByDescending(g => g.Key.Data);

            foreach (var grupa in grupowane)
            {
                var rej = grupa.OrderBy(r => r.DataCzas).ToList();
                var pierwsza = rej.First();

                var portierniaRej = rej.Where(r => r.TypPunktu == "Portiernia").ToList();
                var wejscia = (portierniaRej.Any() ? portierniaRej : rej).Where(r => r.Typ == "Wejście").ToList();
                var wyjscia = (portierniaRej.Any() ? portierniaRej : rej).Where(r => r.Typ == "Wyjście").ToList();

                string pierwszeWejscie = wejscia.Any() ? wejscia.First().DataCzas.ToString("HH:mm") : "-";
                string ostatnieWyjscie = wyjscia.Any() ? wyjscia.Last().DataCzas.ToString("HH:mm") : "-";

                decimal godzinyNaMiejscu = 0;
                decimal godzinyPrzerw = 0;
                int liczbaPrzerw = 0;

                if (wejscia.Any() && wyjscia.Any())
                {
                    var start = wejscia.First().DataCzas;
                    var koniec = wyjscia.Last().DataCzas;
                    godzinyNaMiejscu = (decimal)(koniec - start).TotalHours;

                    var wszystkie = (portierniaRej.Any() ? portierniaRej : rej).OrderBy(r => r.DataCzas).ToList();
                    for (int i = 0; i < wszystkie.Count - 1; i++)
                    {
                        if (wszystkie[i].Typ == "Wyjście" && wszystkie[i + 1].Typ == "Wejście")
                        {
                            var przerwa = (decimal)(wszystkie[i + 1].DataCzas - wszystkie[i].DataCzas).TotalHours;
                            if (przerwa > 0.05m && przerwa < 2)
                            {
                                godzinyPrzerw += przerwa;
                                liczbaPrzerw++;
                            }
                        }
                    }
                }

                var godzinyEfektywne = Math.Max(0, godzinyNaMiejscu - godzinyPrzerw);

                string status = "OK";
                if (!wyjscia.Any()) status = "⚠️ Brak wyjścia";
                else if (!wejscia.Any()) status = "⚠️ Brak wejścia";
                else if (godzinyEfektywne > 12) status = "⚠️ >12h";
                else if (godzinyEfektywne < 4 && godzinyEfektywne > 0) status = "⚡ <4h";

                godzinyPracy.Add(new DzienPracy
                {
                    Data = grupa.Key.Data,
                    IdPracownika = grupa.Key.IdPracownika,
                    Pracownik = pierwsza.Pracownik,
                    Grupa = pierwsza.Grupa,
                    PierwszeWejscie = pierwszeWejscie,
                    OstatnieWyjscie = ostatnieWyjscie,
                    GodzinyNaMiejscu = Math.Round(godzinyNaMiejscu, 2),
                    CzasPracy = FormatGodziny(godzinyNaMiejscu),
                    LiczbaPrzerw = liczbaPrzerw,
                    GodzinyPrzerw = Math.Round(godzinyPrzerw, 2),
                    CzasPrzerw = FormatGodziny(godzinyPrzerw),
                    GodzinyEfektywne = Math.Round(godzinyEfektywne, 2),
                    CzasEfektywny = FormatGodziny(godzinyEfektywne),
                    Status = status
                });
            }
        }

        private string FormatGodziny(decimal godziny)
        {
            var h = (int)godziny;
            var m = (int)((godziny - h) * 60);
            return $"{h}h {m:D2}m";
        }

        private void ObliczObecnych()
        {
            var dzisiaj = DateTime.Today;
            var ostatnieWejscia = rejestracje
                .Where(r => r.DataCzas.Date == dzisiaj && r.TypPunktu == "Portiernia")
                .GroupBy(r => r.IdPracownika)
                .Select(g => g.OrderByDescending(r => r.DataCzas).First())
                .Where(r => r.Typ == "Wejście");

            foreach (var r in ostatnieWejscia)
            {
                var czasNaTerenie = DateTime.Now - r.DataCzas;
                obecni.Add(new StatusObecnosci
                {
                    IdPracownika = r.IdPracownika,
                    Pracownik = r.Pracownik,
                    Grupa = r.Grupa,
                    Godzina = r.Godzina,
                    CzasNaTerenie = $"{(int)czasNaTerenie.TotalHours}h {czasNaTerenie.Minutes:D2}m",
                    PunktDostepu = r.PunktDostepu
                });
            }

            txtLiczbaObecnych.Text = $"{obecni.Count} obecnych";
        }

        private void ObliczPodsumowania()
        {
            var grupowane = godzinyPracy.GroupBy(g => g.Grupa);
            foreach (var grupa in grupowane)
            {
                var pracownicy = grupa.Select(g => g.IdPracownika).Distinct().Count();
                var sumaGodzin = grupa.Sum(g => g.GodzinyEfektywne);
                var sumaPrzerw = grupa.Sum(g => g.GodzinyPrzerw);
                var obecnychTeraz = obecni.Count(o => o.Grupa == grupa.Key);
                var brakiWyjsc = grupa.Count(g => g.Status.Contains("Brak wyjścia"));
                var przekroczenia = grupa.Count(g => g.Status.Contains(">12h"));

                podsumowania.Add(new PodsumowanieAgencji
                {
                    Grupa = grupa.Key,
                    LiczbaPracownikow = pracownicy,
                    SumaGodzin = sumaGodzin,
                    SredniaGodzin = pracownicy > 0 ? sumaGodzin / pracownicy : 0,
                    SumaPrzerw = sumaPrzerw,
                    Obecni = obecnychTeraz,
                    BrakiWyjsc = brakiWyjsc,
                    Przekroczenia = przekroczenia
                });
            }
        }

        private void GenerujAlerty()
        {
            foreach (var d in godzinyPracy.Where(g => g.Status != "OK"))
            {
                alerty.Add(new AlertCzasuPracy
                {
                    Typ = d.Status.Contains("Brak") ? "Brak rejestracji" : "Przekroczenie czasu",
                    Priorytet = d.Status.Contains(">12h") ? "🔴 Wysoki" : "🟡 Średni",
                    Pracownik = d.Pracownik,
                    Grupa = d.Grupa,
                    Data = d.Data,
                    Opis = d.Status
                });
            }
        }

        private void UpdateStats()
        {
            UpdateDateRangeDisplay();
            GenerujRanking();
            GenerujPorownanieAgencji();
            GenerujNadgodziny();
            GenerujAnalizePunktualnosci();
            GenerujNieobecnosci();
            AktualizujListePracownikow();
            ApplyFilters();
        }

        private void UpdateDateRangeDisplay()
        {
            var od = dpOd.SelectedDate ?? DateTime.Today;
            var doo = dpDo.SelectedDate ?? DateTime.Today;
            if (od == doo && od == DateTime.Today)
                txtZakresDat.Text = "Zakres: dziś";
            else if (od == doo)
                txtZakresDat.Text = $"Zakres: {od:dd.MM.yyyy}";
            else
                txtZakresDat.Text = $"Zakres: {od:dd.MM} — {doo:dd.MM.yyyy}";
        }

        private void ApplyFilters()
        {
            var wybranaGrupa = cmbGrupa.SelectedItem as GrupaUnicard;
            var szukaj = txtSzukaj.Text?.ToLower() ?? "";

            var filteredRej = rejestracje.AsEnumerable();
            if (wybranaGrupa != null && wybranaGrupa.Id != 0)
                filteredRej = filteredRej.Where(r => r.Grupa == wybranaGrupa.Nazwa);
            if (!string.IsNullOrEmpty(szukaj))
                filteredRej = filteredRej.Where(r => r.Pracownik.ToLower().Contains(szukaj) || r.Grupa.ToLower().Contains(szukaj));
            gridRejestracje.ItemsSource = new ObservableCollection<RejestrKarty>(filteredRej);

            var filteredGodz = godzinyPracy.AsEnumerable();
            if (wybranaGrupa != null && wybranaGrupa.Id != 0)
                filteredGodz = filteredGodz.Where(g => g.Grupa == wybranaGrupa.Nazwa);
            if (!string.IsNullOrEmpty(szukaj))
                filteredGodz = filteredGodz.Where(g => g.Pracownik.ToLower().Contains(szukaj) || g.Grupa.ToLower().Contains(szukaj));
            gridGodzinyPracy.ItemsSource = new ObservableCollection<DzienPracy>(filteredGodz);
        }

        // Event handlers
        private void ChkTylkoDzisiaj_Changed(object sender, RoutedEventArgs e)
        {
            if (chkTylkoDzisiaj.IsChecked == true)
            {
                isLoading = true;
                dpOd.SelectedDate = DateTime.Today;
                dpDo.SelectedDate = DateTime.Today;
                isLoading = false;
                _ = LoadAllData();
            }
        }

        private async void DpOd_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { if (!isLoading) { chkTylkoDzisiaj.IsChecked = false; await LoadAllData(); } }
        private async void DpDo_SelectedDateChanged(object sender, SelectionChangedEventArgs e) { if (!isLoading) { chkTylkoDzisiaj.IsChecked = false; await LoadAllData(); } }
        private void CmbGrupa_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
        private void BtnWyczyscSzukaj_Click(object sender, RoutedEventArgs e) { txtSzukaj.Text = ""; cmbGrupa.SelectedIndex = 0; }
        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) => await LoadAllData();

        private async void BtnWczoraj_Click(object sender, RoutedEventArgs e)
        {
            isLoading = true;
            dpOd.SelectedDate = DateTime.Today.AddDays(-1);
            dpDo.SelectedDate = DateTime.Today.AddDays(-1);
            chkTylkoDzisiaj.IsChecked = false;
            isLoading = false;
            await LoadAllData();
        }

        private async void BtnTydzien_Click(object sender, RoutedEventArgs e)
        {
            isLoading = true;
            dpOd.SelectedDate = DateTime.Today.AddDays(-7);
            dpDo.SelectedDate = DateTime.Today;
            chkTylkoDzisiaj.IsChecked = false;
            isLoading = false;
            await LoadAllData();
        }

        private async void BtnMiesiac_Click(object sender, RoutedEventArgs e)
        {
            isLoading = true;
            dpOd.SelectedDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            dpDo.SelectedDate = DateTime.Today;
            chkTylkoDzisiaj.IsChecked = false;
            isLoading = false;
            await LoadAllData();
        }

        private void CmbMiesiac_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void CmbRok_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void GenerujRanking()
        {
            ranking.Clear();
            if (godzinyPracy.Count == 0) return;

            var grupowane = godzinyPracy.GroupBy(g => g.IdPracownika);
            var lista = new List<RankingPracownika>();

            foreach (var osoba in grupowane)
            {
                var dni = osoba.ToList();
                var pierwsza = dni.First();
                var dniOK = dni.Count(d => d.Status == "OK");
                var punktualnosc = dni.Count > 0 ? (dniOK * 100.0 / dni.Count) : 100;

                string ocena;
                if (punktualnosc >= 95 && dni.Average(d => (double)d.GodzinyEfektywne) >= 7.5) ocena = "⭐⭐⭐ Wzorowy";
                else if (punktualnosc >= 80 && dni.Average(d => (double)d.GodzinyEfektywne) >= 6) ocena = "⭐⭐ Dobry";
                else if (punktualnosc >= 60) ocena = "⭐ Przeciętny";
                else ocena = "⚠️ Wymaga uwagi";

                lista.Add(new RankingPracownika
                {
                    IdPracownika = osoba.Key,
                    Pracownik = pierwsza.Pracownik,
                    Grupa = pierwsza.Grupa,
                    DniPracy = dni.Count,
                    SumaGodzin = dni.Sum(d => d.GodzinyEfektywne),
                    SredniaGodzin = dni.Average(d => d.GodzinyEfektywne),
                    SumaPrzerw = dni.Sum(d => d.GodzinyPrzerw),
                    Punktualnosc = $"{punktualnosc:N0}%",
                    Ocena = ocena
                });
            }

            int poz = 1;
            foreach (var r in lista.OrderByDescending(r => r.SumaGodzin))
            {
                r.Pozycja = poz++;
                ranking.Add(r);
            }
        }

        private void GenerujPorownanieAgencji()
        {
            porownanieAgencji.Clear();
            if (godzinyPracy.Count == 0) return;

            var grupowane = godzinyPracy.GroupBy(g => g.Grupa);
            var lista = new List<PorownanieAgencjiModel>();

            foreach (var agencja in grupowane)
            {
                var dni = agencja.ToList();
                var pracownicy = dni.Select(d => d.IdPracownika).Distinct().Count();
                var dniOK = dni.Count(d => d.Status == "OK");
                var punktualnosc = dni.Count > 0 ? (dniOK * 100.0 / dni.Count) : 100;
                var sredniaGodzin = pracownicy > 0 ? dni.Sum(d => d.GodzinyEfektywne) / pracownicy : 0;

                string ocena;
                if (punktualnosc >= 90 && (double)sredniaGodzin >= 7) ocena = "⭐⭐⭐ Świetna";
                else if (punktualnosc >= 75 && (double)sredniaGodzin >= 6) ocena = "⭐⭐ Dobra";
                else if (punktualnosc >= 50) ocena = "⭐ Przeciętna";
                else ocena = "⚠️ Słaba";

                lista.Add(new PorownanieAgencjiModel
                {
                    Grupa = agencja.Key,
                    LiczbaPracownikow = pracownicy,
                    SumaGodzin = dni.Sum(d => d.GodzinyEfektywne),
                    SredniaNaOsobe = sredniaGodzin,
                    Frekwencja = "100%",
                    Punktualnosc = $"{punktualnosc:N0}%",
                    SredniePrzerwy = pracownicy > 0 ? dni.Sum(d => d.GodzinyPrzerw) / pracownicy : 0,
                    Problemy = dni.Count(d => d.Status != "OK"),
                    Ocena = ocena
                });
            }

            int poz = 1;
            foreach (var p in lista.OrderByDescending(p => p.SumaGodzin))
            {
                p.Pozycja = poz++;
                porownanieAgencji.Add(p);
            }
        }

        private void BtnOdswiezPorownanie_Click(object sender, RoutedEventArgs e) => GenerujPorownanieAgencji();

        private void GenerujNadgodziny()
        {
            nadgodziny.Clear();
            if (godzinyPracy.Count == 0) return;

            var grupowane = godzinyPracy.GroupBy(g => g.IdPracownika);

            foreach (var osoba in grupowane)
            {
                var dni = osoba.ToList();
                var pierwsza = dni.First();

                decimal nadgodzinyDzien = dni.Where(d => d.GodzinyEfektywne > 8).Sum(d => d.GodzinyEfektywne - 8);
                decimal nadgodzinyTydzien = 0;

                var tygodnie = dni.GroupBy(d => System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
                    d.Data, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday));
                foreach (var tydzien in tygodnie)
                {
                    var suma = tydzien.Sum(d => d.GodzinyEfektywne);
                    if (suma > 40) nadgodzinyTydzien += suma - 40;
                }

                var nadgodzinyOkres = Math.Max(nadgodzinyDzien, nadgodzinyTydzien);
                var procentLimitu = (nadgodzinyOkres / 150m) * 100m;

                string status;
                if (procentLimitu >= 100) status = "❌ PRZEKROCZONY!";
                else if (procentLimitu >= 80) status = "⚠️ Blisko limitu";
                else if (procentLimitu >= 50) status = "⚡ Monitorować";
                else status = "✅ OK";

                if (nadgodzinyOkres > 0)
                {
                    nadgodziny.Add(new NadgodzinyPracownika
                    {
                        IdPracownika = osoba.Key,
                        Pracownik = pierwsza.Pracownik,
                        Grupa = pierwsza.Grupa,
                        NadgodzinyDzien = nadgodzinyDzien,
                        NadgodzinyTydzien = nadgodzinyTydzien,
                        NadgodzinyMiesiac = nadgodzinyOkres,
                        NadgodzinyRok = nadgodzinyOkres,
                        ProcentLimitu = procentLimitu,
                        StatusLimitu = status
                    });
                }
            }
        }

        private void GenerujAnalizePunktualnosci()
        {
            analizaPunktualnosci.Clear();
            if (godzinyPracy.Count == 0) return;

            var grupowane = godzinyPracy.GroupBy(g => g.IdPracownika);

            foreach (var osoba in grupowane)
            {
                var dni = osoba.ToList();
                var pierwsza = dni.First();

                int spoznienia = 0, sumaSpoznienMin = 0, wczesniejszeWyjscia = 0;

                foreach (var dzien in dni)
                {
                    if (dzien.PierwszeWejscie != "-" && TimeSpan.TryParse(dzien.PierwszeWejscie, out var wejscie))
                    {
                        if (wejscie > godzinaPoczatkuPracy)
                        {
                            spoznienia++;
                            sumaSpoznienMin += (int)(wejscie - godzinaPoczatkuPracy).TotalMinutes;
                        }
                    }
                    if (dzien.OstatnieWyjscie != "-" && TimeSpan.TryParse(dzien.OstatnieWyjscie, out var wyjscie))
                    {
                        if (wyjscie < godzinaKoncaPracy && dzien.GodzinyEfektywne < 8) wczesniejszeWyjscia++;
                    }
                }

                var dniOK = dni.Count - spoznienia - wczesniejszeWyjscia;
                var punktualnosc = dni.Count > 0 ? (dniOK * 100m / dni.Count) : 100;

                string ocena;
                if (punktualnosc >= 95) ocena = "⭐⭐⭐ Wzorowa";
                else if (punktualnosc >= 85) ocena = "⭐⭐ Dobra";
                else if (punktualnosc >= 70) ocena = "⭐ Przeciętna";
                else ocena = "⚠️ Wymaga poprawy";

                string trend = spoznienia == 0 ? "📈" : spoznienia <= 2 ? "➡️" : "📉";

                analizaPunktualnosci.Add(new AnalizaPunktualnosci
                {
                    IdPracownika = osoba.Key,
                    Pracownik = pierwsza.Pracownik,
                    Grupa = pierwsza.Grupa,
                    DniPracy = dni.Count,
                    Spoznienia = spoznienia,
                    SumaSpoznienMin = sumaSpoznienMin,
                    WczesniejszeWyjscia = wczesniejszeWyjscia,
                    ProcentPunktualnosci = punktualnosc,
                    Ocena = ocena,
                    Trend = trend
                });
            }
        }

        private void GenerujNieobecnosci()
        {
            nieobecnosci.Clear();
            if (godzinyPracy.Count == 0 || !dpOd.SelectedDate.HasValue || !dpDo.SelectedDate.HasValue) return;

            var wszyscy = godzinyPracy.Select(g => new { g.IdPracownika, g.Pracownik, g.Grupa }).Distinct().ToList();

            for (var data = dpOd.SelectedDate.Value; data <= dpDo.SelectedDate.Value; data = data.AddDays(1))
            {
                if (data.DayOfWeek == DayOfWeek.Saturday || data.DayOfWeek == DayOfWeek.Sunday || swietaPolskie.Contains(data.Date))
                    continue;

                var obecniDnia = godzinyPracy.Where(g => g.Data.Date == data.Date).Select(g => g.IdPracownika).ToHashSet();

                foreach (var prac in wszyscy.Where(p => !obecniDnia.Contains(p.IdPracownika)))
                {
                    nieobecnosci.Add(new Nieobecnosc
                    {
                        IdPracownika = prac.IdPracownika,
                        Pracownik = prac.Pracownik,
                        Grupa = prac.Grupa,
                        Data = data,
                        TypNieobecnosci = "❓ Nieusprawiedliwiona",
                        Status = "Do wyjaśnienia",
                        Uwagi = "Brak rejestracji"
                    });
                }
            }

            txtNieobecnosciNieusp.Text = nieobecnosci.Count(n => n.TypNieobecnosci.Contains("Nieusprawiedliwiona")).ToString();
            txtNieobecnosciChoroba.Text = nieobecnosci.Count(n => n.TypNieobecnosci.Contains("Choroba")).ToString();
            txtNieobecnosciUrlop.Text = nieobecnosci.Count(n => n.TypNieobecnosci.Contains("Urlop")).ToString();
        }

        private void AktualizujListePracownikow()
        {
            cmbPracownikEwidencja.Items.Clear();
            var pracownicy = godzinyPracy.Select(g => new { g.IdPracownika, g.Pracownik, g.Grupa }).Distinct().OrderBy(p => p.Pracownik);
            foreach (var p in pracownicy) cmbPracownikEwidencja.Items.Add(new ComboBoxItem { Content = p.Pracownik, Tag = p.IdPracownika });
            if (cmbPracownikEwidencja.Items.Count > 0) cmbPracownikEwidencja.SelectedIndex = 0;
        }

        private void CmbPracownikEwidencja_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private async void BtnGenerujKarteEwidencji_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPracownikEwidencja.SelectedItem == null) return;

            var pracownikItem = cmbPracownikEwidencja.SelectedItem as ComboBoxItem;
            var miesiacItem = cmbMiesiacEwidencja.SelectedItem as ComboBoxItem;
            if (pracownikItem == null || miesiacItem == null || cmbRokEwidencja.SelectedItem == null) return;

            int idPracownika = (int)pracownikItem.Tag;
            int numMies = (int)miesiacItem.Tag;
            int rok = (int)cmbRokEwidencja.SelectedItem;

            var pierwszyDzien = new DateTime(rok, numMies, 1);
            var ostatniDzien = pierwszyDzien.AddMonths(1).AddDays(-1);

            isLoading = true;
            dpOd.SelectedDate = pierwszyDzien;
            dpDo.SelectedDate = ostatniDzien;
            chkTylkoDzisiaj.IsChecked = false;
            isLoading = false;

            await LoadAllData();
            GenerujKarteEwidencjiDane(idPracownika, pierwszyDzien, ostatniDzien);
        }

        private void GenerujKarteEwidencjiDane(int idPracownika, DateTime odDaty, DateTime doDaty)
        {
            kartaEwidencji.Clear();
            var dniPrac = godzinyPracy.Where(g => g.IdPracownika == idPracownika).ToDictionary(g => g.Data.Date);
            var dnTyg = new[] { "Nd", "Pn", "Wt", "Śr", "Cz", "Pt", "Sb" };

            decimal sumaNorm = 0, sumaNadg = 0, sumaNoc = 0;

            for (var data = odDaty; data <= doDaty; data = data.AddDays(1))
            {
                var dzien = new DzienEwidencji
                {
                    Data = data,
                    DzienTygodnia = dnTyg[(int)data.DayOfWeek],
                    Weekend = data.DayOfWeek == DayOfWeek.Saturday || data.DayOfWeek == DayOfWeek.Sunday,
                    Swieto = swietaPolskie.Contains(data.Date)
                };

                if (dniPrac.TryGetValue(data.Date, out var dp))
                {
                    dzien.Wejscie = dp.PierwszeWejscie;
                    dzien.Wyjscie = dp.OstatnieWyjscie;
                    dzien.GodzinyNormalne = Math.Min(dp.GodzinyEfektywne, 8);
                    dzien.Nadgodziny = Math.Max(0, dp.GodzinyEfektywne - 8);
                    dzien.GodzinyNocne = 0;
                    if (dzien.Weekend) dzien.Uwagi = "Weekend";
                    else if (dzien.Swieto) dzien.Uwagi = "Święto";
                    else if (dp.Status != "OK") dzien.Uwagi = dp.Status;

                    sumaNorm += dzien.GodzinyNormalne;
                    sumaNadg += dzien.Nadgodziny;
                }
                else
                {
                    dzien.Wejscie = "-";
                    dzien.Wyjscie = "-";
                    if (dzien.Weekend) dzien.Uwagi = "Weekend";
                    else if (dzien.Swieto) dzien.Uwagi = "Święto";
                    else dzien.Uwagi = "Nieobecność";
                }

                kartaEwidencji.Add(dzien);
            }

            txtEwidencjaDni.Text = kartaEwidencji.Count(k => k.GodzinyNormalne > 0).ToString();
            txtEwidencjaNormalne.Text = $"{sumaNorm:N1}h";
            txtEwidencjaNadgodziny.Text = $"{sumaNadg:N1}h";
            txtEwidencjaNocne.Text = $"{sumaNoc:N1}h";
        }

        private async void BtnGenerujRaportMiesieczny_Click(object sender, RoutedEventArgs e)
        {
            var miesiacItem = cmbMiesiac.SelectedItem as ComboBoxItem;
            if (miesiacItem == null || cmbRok.SelectedItem == null) return;

            int numMies = (int)miesiacItem.Tag;
            int rok = (int)cmbRok.SelectedItem;
            var pierwszyDzien = new DateTime(rok, numMies, 1);
            var ostatniDzien = pierwszyDzien.AddMonths(1).AddDays(-1);

            isLoading = true;
            dpOd.SelectedDate = pierwszyDzien;
            dpDo.SelectedDate = ostatniDzien;
            chkTylkoDzisiaj.IsChecked = false;
            isLoading = false;

            await LoadAllData();
            GenerujRaportMiesiecznyDane();
        }

        private void GenerujRaportMiesiecznyDane()
        {
            raportMiesieczny.Clear();
            if (godzinyPracy.Count == 0) return;

            var grupowane = godzinyPracy.GroupBy(g => g.IdPracownika);

            foreach (var osoba in grupowane)
            {
                var dni = osoba.ToList();
                var pierwsza = dni.First();
                var braki = dni.Count(d => d.Status != "OK");

                string status;
                if (braki == 0) status = "✅ Kompletny";
                else if (braki <= 2) status = $"⚠️ {braki} braki";
                else status = $"❌ {braki} problemów";

                raportMiesieczny.Add(new RaportMiesiecznyPracownika
                {
                    IdPracownika = osoba.Key,
                    Pracownik = pierwsza.Pracownik,
                    Grupa = pierwsza.Grupa,
                    DniPracy = dni.Count,
                    GodzinyEfektywne = dni.Sum(d => d.GodzinyEfektywne),
                    GodzinyPrzerw = dni.Sum(d => d.GodzinyPrzerw),
                    SredniaDzien = dni.Average(d => d.GodzinyEfektywne),
                    NajwczesniejszeWejscie = dni.Where(d => d.PierwszeWejscie != "-").OrderBy(d => d.PierwszeWejscie).FirstOrDefault()?.PierwszeWejscie ?? "-",
                    NajpozniejWyjscie = dni.Where(d => d.OstatnieWyjscie != "-").OrderByDescending(d => d.OstatnieWyjscie).FirstOrDefault()?.OstatnieWyjscie ?? "-",
                    Braki = braki,
                    Status = status
                });
            }
        }

        // Double-click handlers
        private void GridRejestracje_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridRejestracje.SelectedItem is RejestrKarty r) PokazSzczegolyPracownika(r.IdPracownika, r.Pracownik);
        }

        private void GridGodzinyPracy_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridGodzinyPracy.SelectedItem is DzienPracy d) PokazSzczegolyPracownika(d.IdPracownika, d.Pracownik);
        }

        private void GridRanking_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridRanking.SelectedItem is RankingPracownika r) PokazSzczegolyPracownika(r.IdPracownika, r.Pracownik);
        }

        private void GridRaportMiesieczny_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridRaportMiesieczny.SelectedItem is RaportMiesiecznyPracownika r) PokazSzczegolyPracownika(r.IdPracownika, r.Pracownik);
        }

        private void GridPunktualnosc_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (gridPunktualnosc.SelectedItem is AnalizaPunktualnosci a) PokazSzczegolyPracownika(a.IdPracownika, a.Pracownik);
        }

        private void PokazSzczegolyPracownika(int idPracownika, string nazwaPracownika)
        {
            var dniPracownika = godzinyPracy.Where(g => g.IdPracownika == idPracownika).OrderBy(g => g.Data).ToList();
            var rejPracownika = rejestracje.Where(r => r.IdPracownika == idPracownika).OrderByDescending(r => r.DataCzas).ToList();

            if (!dniPracownika.Any() && !rejPracownika.Any())
            {
                MessageBox.Show("Brak danych dla tego pracownika.", "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"  {nazwaPracownika.ToUpper()}");
            sb.AppendLine($"═══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            if (dniPracownika.Any())
            {
                var pierwsza = dniPracownika.First();
                sb.AppendLine($"  Dział:          {pierwsza.Grupa}");
                sb.AppendLine($"  Okres:          {dpOd.SelectedDate:dd.MM.yyyy} — {dpDo.SelectedDate:dd.MM.yyyy}");
                sb.AppendLine($"  Dni pracy:      {dniPracownika.Count}");
                sb.AppendLine($"  Godz. efekt.:   {dniPracownika.Sum(d => d.GodzinyEfektywne):N1}h");
                sb.AppendLine($"  Godz. przerw:   {dniPracownika.Sum(d => d.GodzinyPrzerw):N1}h");
                sb.AppendLine($"  Średnia/dzień:  {dniPracownika.Average(d => d.GodzinyEfektywne):N2}h");
                sb.AppendLine();
                sb.AppendLine($"  {"DATA",-12} {"WEJŚCIE",-10} {"WYJŚCIE",-10} {"EFEKT.",-10} {"STATUS"}");
                sb.AppendLine($"  ─────────────────────────────────────────────────────────────");

                foreach (var d in dniPracownika)
                    sb.AppendLine($"  {d.Data:dd.MM.yyyy}   {d.PierwszeWejscie,-10} {d.OstatnieWyjscie,-10} {d.CzasEfektywny,-10} {d.Status}");
            }

            var window = new Window
            {
                Title = $"Szczegóły: {nazwaPracownika}",
                Width = 750, Height = 600,
                Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1e1e2e")),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var textBox = new TextBox
            {
                Text = sb.ToString(),
                IsReadOnly = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Background = System.Windows.Media.Brushes.Transparent,
                Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#cdd6f4")),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(20),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            window.Content = textBox;
            window.ShowDialog();
        }

        // Print and export
        private void DrukujTekst(string tekst, string tytul)
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                var flowDoc = new FlowDocument
                {
                    PageWidth = printDialog.PrintableAreaWidth,
                    PageHeight = printDialog.PrintableAreaHeight,
                    PagePadding = new Thickness(40),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 9
                };
                flowDoc.Blocks.Add(new Paragraph(new Run(tekst)));
                printDialog.PrintDocument(((IDocumentPaginatorSource)flowDoc).DocumentPaginator, tytul);
            }
        }

        private void BtnDrukujRaport_Click(object sender, RoutedEventArgs e)
        {
            if (raportMiesieczny.Count == 0) { MessageBox.Show("Najpierw wygeneruj raport.", "Info", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("RAPORT MIESIĘCZNY");
            sb.AppendLine($"{"Pracownik",-25} {"Dział",-15} {"Dni",-5} {"Godz.",-8} {"Status"}");
            sb.AppendLine(new string('─', 70));
            foreach (var r in raportMiesieczny)
                sb.AppendLine($"{r.Pracownik,-25} {r.Grupa,-15} {r.DniPracy,-5} {r.GodzinyEfektywne,-8:N1} {r.Status}");
            DrukujTekst(sb.ToString(), "Raport Miesięczny");
        }

        private void BtnDrukujPorownanie_Click(object sender, RoutedEventArgs e)
        {
            if (porownanieAgencji.Count == 0) { MessageBox.Show("Brak danych.", "Info", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("PORÓWNANIE AGENCJI");
            sb.AppendLine($"{"#",-3} {"Agencja",-20} {"Prac.",-6} {"Godz.",-8} {"Ocena"}");
            sb.AppendLine(new string('─', 50));
            foreach (var a in porownanieAgencji)
                sb.AppendLine($"{a.Pozycja,-3} {a.Grupa,-20} {a.LiczbaPracownikow,-6} {a.SumaGodzin,-8:N1} {a.Ocena}");
            DrukujTekst(sb.ToString(), "Porównanie Agencji");
        }

        private void BtnDrukujKarteEwidencji_Click(object sender, RoutedEventArgs e)
        {
            if (kartaEwidencji.Count == 0) { MessageBox.Show("Najpierw wygeneruj kartę.", "Info", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("KARTA EWIDENCJI CZASU PRACY");
            sb.AppendLine($"{"Data",-10} {"Dz.",-4} {"Wej.",-8} {"Wyj.",-8} {"Norm.",-8} {"Nadg.",-8} {"Uwagi"}");
            sb.AppendLine(new string('─', 70));
            foreach (var d in kartaEwidencji)
                sb.AppendLine($"{d.Data:dd.MM}     {d.DzienTygodnia,-4} {d.Wejscie,-8} {d.Wyjscie,-8} {d.GodzinyNormalne,-8:N1} {d.Nadgodziny,-8:N1} {d.Uwagi}");
            sb.AppendLine();
            sb.AppendLine($"Dni: {txtEwidencjaDni.Text} | Normalne: {txtEwidencjaNormalne.Text} | Nadgodziny: {txtEwidencjaNadgodziny.Text}");
            DrukujTekst(sb.ToString(), "Karta Ewidencji");
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV|*.csv", FileName = $"CzasPracy_{DateTime.Now:yyyyMMdd}.csv" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    using (var writer = new StreamWriter(dialog.FileName, false, System.Text.Encoding.UTF8))
                    {
                        writer.WriteLine("Data;Pracownik;Dział;Wejście;Wyjście;Na miejscu;Przerwy;Efektywnie;Status");
                        foreach (var g in godzinyPracy)
                            writer.WriteLine($"{g.Data:dd.MM.yyyy};{g.Pracownik};{g.Grupa};{g.PierwszeWejscie};{g.OstatnieWyjscie};{g.CzasPracy};{g.CzasPrzerw};{g.CzasEfektywny};{g.Status}");
                    }
                    MessageBox.Show($"Wyeksportowano do:\n{dialog.FileName}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Błąd: {ex.Message}", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }
    }

    // Models
    public class GrupaUnicard { public int Id { get; set; } public string Nazwa { get; set; } }

    public class RejestrKarty
    {
        public DateTime DataCzas { get; set; }
        public string Godzina { get; set; }
        public string Typ { get; set; }
        public int IdPracownika { get; set; }
        public string Pracownik { get; set; }
        public string Grupa { get; set; }
        public string PunktDostepu { get; set; }
        public string IdKarty { get; set; }
        public string TypPunktu { get; set; }
    }

    public class DzienPracy
    {
        public DateTime Data { get; set; }
        public int IdPracownika { get; set; }
        public string Pracownik { get; set; }
        public string Grupa { get; set; }
        public string PierwszeWejscie { get; set; }
        public string OstatnieWyjscie { get; set; }
        public decimal GodzinyNaMiejscu { get; set; }
        public string CzasPracy { get; set; }
        public int LiczbaPrzerw { get; set; }
        public decimal GodzinyPrzerw { get; set; }
        public string CzasPrzerw { get; set; }
        public decimal GodzinyEfektywne { get; set; }
        public string CzasEfektywny { get; set; }
        public string Status { get; set; }
    }

    public class StatusObecnosci
    {
        public int IdPracownika { get; set; }
        public string Pracownik { get; set; }
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

    public class RankingPracownika
    {
        public int Pozycja { get; set; }
        public int IdPracownika { get; set; }
        public string Pracownik { get; set; }
        public string Grupa { get; set; }
        public int DniPracy { get; set; }
        public decimal SumaGodzin { get; set; }
        public decimal SredniaGodzin { get; set; }
        public decimal SumaPrzerw { get; set; }
        public string Punktualnosc { get; set; }
        public string Ocena { get; set; }
    }

    public class RaportMiesiecznyPracownika
    {
        public int IdPracownika { get; set; }
        public string Pracownik { get; set; }
        public string Grupa { get; set; }
        public int DniPracy { get; set; }
        public decimal GodzinyEfektywne { get; set; }
        public decimal GodzinyPrzerw { get; set; }
        public decimal SredniaDzien { get; set; }
        public string NajwczesniejszeWejscie { get; set; }
        public string NajpozniejWyjscie { get; set; }
        public int Braki { get; set; }
        public string Status { get; set; }
    }

    public class PorownanieAgencjiModel
    {
        public int Pozycja { get; set; }
        public string Grupa { get; set; }
        public int LiczbaPracownikow { get; set; }
        public decimal SumaGodzin { get; set; }
        public decimal SredniaNaOsobe { get; set; }
        public string Frekwencja { get; set; }
        public string Punktualnosc { get; set; }
        public decimal SredniePrzerwy { get; set; }
        public int Problemy { get; set; }
        public string Ocena { get; set; }
    }

    public class NadgodzinyPracownika
    {
        public int IdPracownika { get; set; }
        public string Pracownik { get; set; }
        public string Grupa { get; set; }
        public decimal NadgodzinyDzien { get; set; }
        public decimal NadgodzinyTydzien { get; set; }
        public decimal NadgodzinyMiesiac { get; set; }
        public decimal NadgodzinyRok { get; set; }
        public decimal ProcentLimitu { get; set; }
        public string StatusLimitu { get; set; }
    }

    public class DzienEwidencji
    {
        public DateTime Data { get; set; }
        public string DzienTygodnia { get; set; }
        public string Wejscie { get; set; }
        public string Wyjscie { get; set; }
        public decimal GodzinyNormalne { get; set; }
        public decimal Nadgodziny { get; set; }
        public decimal GodzinyNocne { get; set; }
        public bool Weekend { get; set; }
        public bool Swieto { get; set; }
        public string Uwagi { get; set; }
    }

    public class AnalizaPunktualnosci
    {
        public int IdPracownika { get; set; }
        public string Pracownik { get; set; }
        public string Grupa { get; set; }
        public int DniPracy { get; set; }
        public int Spoznienia { get; set; }
        public int SumaSpoznienMin { get; set; }
        public int WczesniejszeWyjscia { get; set; }
        public decimal ProcentPunktualnosci { get; set; }
        public string Ocena { get; set; }
        public string Trend { get; set; }
    }

    public class Nieobecnosc
    {
        public int IdPracownika { get; set; }
        public string Pracownik { get; set; }
        public string Grupa { get; set; }
        public DateTime Data { get; set; }
        public string TypNieobecnosci { get; set; }
        public string Status { get; set; }
        public string Uwagi { get; set; }
    }
}
