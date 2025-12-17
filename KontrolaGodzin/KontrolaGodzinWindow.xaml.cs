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
        // ZMIEN NA SWOJ CONNECTION STRING DO UNICARD!
        private const string connectionUnicard = "Server=192.168.0.109;Database=UNISYSTEM;User Id=sa;Password=Libra!2024;TrustServerCertificate=True";

        private ObservableCollection<RejestrKarty> rejestracje = new ObservableCollection<RejestrKarty>();
        private ObservableCollection<DzienPracy> godzinyPracy = new ObservableCollection<DzienPracy>();
        private ObservableCollection<StatusObecnosci> obecni = new ObservableCollection<StatusObecnosci>();
        private ObservableCollection<PodsumowanieAgencji> podsumowania = new ObservableCollection<PodsumowanieAgencji>();
        private ObservableCollection<AlertCzasuPracy> alerty = new ObservableCollection<AlertCzasuPracy>();
        private List<GrupaUnicard> grupy = new List<GrupaUnicard>();

        private const string SQL_REJESTRACJE = @"
            WITH RejestracjeBezDuplikatow AS (
                SELECT 
                    r.KDINAR_ID,
                    r.KDINAR_DATE_TIME,
                    CASE r.KDINAR_REGISTRTN_TYPE WHEN 1 THEN 'WE' WHEN 2 THEN 'WY' ELSE 'NN' END AS Typ,
                    e.RCINE_ID AS IdPracownika,
                    e.RCINE_FIRST_NAME AS Imie,
                    e.RCINE_LAST_NAME AS Nazwisko,
                    e.RCINE_GROUP_ID AS IdGrupy,
                    r.KDINAR_ACCESS_POINT_NAME AS PunktDostepu,
                    ROW_NUMBER() OVER (
                        PARTITION BY e.RCINE_ID, CONVERT(DATE, r.KDINAR_DATE_TIME),
                                     DATEPART(HOUR, r.KDINAR_DATE_TIME),
                                     DATEPART(MINUTE, r.KDINAR_DATE_TIME),
                                     DATEPART(SECOND, r.KDINAR_DATE_TIME),
                                     r.KDINAR_REGISTRTN_TYPE
                        ORDER BY r.KDINAR_ID
                    ) AS rn
                FROM V_KDINAR_ALL_REGISTRATIONS r
                LEFT JOIN V_RCINE_EMPLOYEES e ON r.KDINAR_PERSON_ID = e.RCINE_ID
                WHERE r.KDINAR_DATE_TIME >= @odDaty AND r.KDINAR_DATE_TIME < @doDaty AND e.RCINE_ID IS NOT NULL
            )
            SELECT * FROM RejestracjeBezDuplikatow WHERE rn = 1 ORDER BY KDINAR_DATE_TIME DESC";

        private const string SQL_OBECNI = @"
            WITH OstatnieZdarzenia AS (
                SELECT e.RCINE_ID, e.RCINE_FIRST_NAME, e.RCINE_LAST_NAME, e.RCINE_GROUP_ID,
                       r.KDINAR_DATE_TIME, r.KDINAR_REGISTRTN_TYPE, r.KDINAR_ACCESS_POINT_NAME,
                       ROW_NUMBER() OVER (PARTITION BY e.RCINE_ID ORDER BY r.KDINAR_DATE_TIME DESC) AS rn
                FROM V_KDINAR_ALL_REGISTRATIONS r
                INNER JOIN V_RCINE_EMPLOYEES e ON r.KDINAR_PERSON_ID = e.RCINE_ID
                WHERE CONVERT(DATE, r.KDINAR_DATE_TIME) = CONVERT(DATE, GETDATE())
            )
            SELECT * FROM OstatnieZdarzenia WHERE rn = 1 AND KDINAR_REGISTRTN_TYPE = 1
            ORDER BY RCINE_LAST_NAME, RCINE_FIRST_NAME";

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
            dpOd.SelectedDate = DateTime.Today;
            dpDo.SelectedDate = DateTime.Today.AddDays(1);
            LoadGrupy();
            await LoadAllData();
        }

        private void ChkTylkoDzisiaj_Changed(object sender, RoutedEventArgs e)
        {
            if (chkTylkoDzisiaj.IsChecked == true)
            {
                dpOd.SelectedDate = DateTime.Today;
                dpDo.SelectedDate = DateTime.Today.AddDays(1);
            }
            dpOd.IsEnabled = chkTylkoDzisiaj.IsChecked != true;
            dpDo.IsEnabled = chkTylkoDzisiaj.IsChecked != true;
        }

        private void CmbGrupa_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
        private void TxtSzukaj_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
        private async void BtnOdswiez_Click(object sender, RoutedEventArgs e) => await LoadAllData();
        private void BtnExport_Click(object sender, RoutedEventArgs e) => ExportToExcel();

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
            btnOdswiez.IsEnabled = false;
            btnOdswiez.Content = "Ladowanie...";
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
                MessageBox.Show($"Blad podczas ladowania danych:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                btnOdswiez.IsEnabled = true;
                btnOdswiez.Content = "Odswiez";
                panelLadowanie.Visibility = Visibility.Collapsed;
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
                        cmd.Parameters.AddWithValue("@doDaty", dpDo.SelectedDate ?? DateTime.Today.AddDays(1));
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var idGrupy = reader["IdGrupy"] != DBNull.Value ? Convert.ToInt32(reader["IdGrupy"]) : 0;
                                var nazwaGrupy = nazwyGrup.ContainsKey(idGrupy) ? nazwyGrup[idGrupy] : $"Grupa {idGrupy}";
                                var imie = reader["Imie"]?.ToString() ?? "";
                                var nazwisko = reader["Nazwisko"]?.ToString() ?? "";
                                var dataCzas = (DateTime)reader["KDINAR_DATE_TIME"];

                                lista.Add(new RejestrKarty
                                {
                                    Id = Convert.ToInt32(reader["KDINAR_ID"]),
                                    DataCzas = dataCzas,
                                    Godzina = dataCzas.ToString("HH:mm:ss"),
                                    Typ = reader["Typ"].ToString(),
                                    IdPracownika = Convert.ToInt32(reader["IdPracownika"]),
                                    Pracownik = $"{imie} {nazwisko}".Trim(),
                                    IdGrupy = idGrupy,
                                    Grupa = nazwaGrupy,
                                    PunktDostepu = reader["PunktDostepu"]?.ToString() ?? ""
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Blad ladowania rejestracji:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                                var idGrupy = reader["RCINE_GROUP_ID"] != DBNull.Value ? Convert.ToInt32(reader["RCINE_GROUP_ID"]) : 0;
                                var nazwaGrupy = nazwyGrup.ContainsKey(idGrupy) ? nazwyGrup[idGrupy] : $"Grupa {idGrupy}";
                                var imie = reader["RCINE_FIRST_NAME"]?.ToString() ?? "";
                                var nazwisko = reader["RCINE_LAST_NAME"]?.ToString() ?? "";
                                var ostatnie = (DateTime)reader["KDINAR_DATE_TIME"];
                                var czasNaTerenie = DateTime.Now - ostatnie;

                                lista.Add(new StatusObecnosci
                                {
                                    IdPracownika = Convert.ToInt32(reader["RCINE_ID"]),
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
                MessageBox.Show($"Blad ladowania obecnych:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                var wejscia = rejestracjeGrupy.Where(r => r.Typ == "WE").ToList();
                var wyjscia = rejestracjeGrupy.Where(r => r.Typ == "WY").ToList();

                var pierwszeWejscie = wejscia.Any() ? wejscia.First().DataCzas : (DateTime?)null;
                var ostatnieWyjscie = wyjscia.Any() ? wyjscia.Last().DataCzas : (DateTime?)null;

                TimeSpan? czasPracy = null;
                string status = "OK";

                if (pierwszeWejscie.HasValue && ostatnieWyjscie.HasValue && ostatnieWyjscie > pierwszeWejscie)
                {
                    czasPracy = ostatnieWyjscie.Value - pierwszeWejscie.Value;
                    if (czasPracy.Value.TotalHours > 14) status = "Ponad 14h";
                }
                else if (!pierwszeWejscie.HasValue) status = "Brak wejscia";
                else if (!ostatnieWyjscie.HasValue) status = "Brak wyjscia";
                else status = "Blad danych";

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
                    LiczbaWejsc = wejscia.Count,
                    LiczbaWyjsc = wyjscia.Count,
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
                var sumaGodzin = grupa.Sum(r => r.GodzinyDziesietne);

                podsumowania.Add(new PodsumowanieAgencji
                {
                    Grupa = grupa.Key,
                    LiczbaPracownikow = pracownicy,
                    SumaGodzin = Math.Round(sumaGodzin, 1),
                    SredniaGodzin = pracownicy > 0 ? Math.Round(sumaGodzin / pracownicy, 1) : 0,
                    Obecni = obecni.Count(r => r.Grupa == grupa.Key),
                    BrakiWyjsc = grupa.Count(r => r.Status.Contains("Brak wyjscia")),
                    Przekroczenia = grupa.Count(r => r.Status.Contains("14h"))
                });
            }
        }

        private void GenerujAlerty()
        {
            alerty.Clear();
            foreach (var dzien in godzinyPracy.Where(d => d.Status != "OK"))
            {
                string typ = dzien.Status.Contains("Brak wejscia") ? "Brak wejscia" :
                             dzien.Status.Contains("Brak wyjscia") ? "Brak wyjscia" :
                             dzien.Status.Contains("14h") ? "Przekroczenie czasu" : "Inny problem";
                string priorytet = dzien.Status.Contains("Brak wejscia") ? "Wysoki" :
                                   dzien.Status.Contains("Brak wyjscia") ? "Sredni" : "Niski";

                alerty.Add(new AlertCzasuPracy
                {
                    Typ = typ, Priorytet = priorytet, Pracownik = dzien.Pracownik,
                    Grupa = dzien.Grupa, Data = dzien.Data, Opis = dzien.Status
                });
            }
        }

        private void UpdateStats()
        {
            txtStatRejestracje.Text = rejestracje.Count.ToString();
            txtStatObecni.Text = obecni.Count.ToString();
            txtStatGodziny.Text = $"{Math.Round(godzinyPracy.Sum(g => g.GodzinyDziesietne), 1)}h";
            txtStatPracownicy.Text = godzinyPracy.Select(g => g.IdPracownika).Distinct().Count().ToString();
            txtStatAlerty.Text = alerty.Count.ToString();
        }

        private void ApplyFilters()
        {
            var wybranaGrupa = cmbGrupa.SelectedItem as GrupaUnicard;
            var szukaj = txtSzukaj.Text?.ToLower() ?? "";

            gridRejestracje.ItemsSource = rejestracje.Where(r =>
                (wybranaGrupa == null || wybranaGrupa.IdGrupy == 0 || r.IdGrupy == wybranaGrupa.IdGrupy) &&
                (string.IsNullOrEmpty(szukaj) || r.Pracownik.ToLower().Contains(szukaj))).ToList();

            gridGodzinyPracy.ItemsSource = godzinyPracy.Where(g =>
                (wybranaGrupa == null || wybranaGrupa.IdGrupy == 0 || g.IdGrupy == wybranaGrupa.IdGrupy) &&
                (string.IsNullOrEmpty(szukaj) || g.Pracownik.ToLower().Contains(szukaj))).ToList();

            gridObecni.ItemsSource = obecni.Where(o =>
                (wybranaGrupa == null || wybranaGrupa.IdGrupy == 0 || o.IdGrupy == wybranaGrupa.IdGrupy) &&
                (string.IsNullOrEmpty(szukaj) || o.Pracownik.ToLower().Contains(szukaj))).ToList();
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
                                writer.WriteLine("Godzina;Typ;Pracownik;Grupa;Punkt dostepu");
                                foreach (var r in rejestracje) writer.WriteLine($"{r.Godzina};{r.Typ};{r.Pracownik};{r.Grupa};{r.PunktDostepu}");
                                break;
                            case 1:
                                writer.WriteLine("Pracownik;Grupa;Data;Wejscie;Wyjscie;Czas pracy;Godziny;Wejsc;Wyjsc;Status");
                                foreach (var g in godzinyPracy) writer.WriteLine($"{g.Pracownik};{g.Grupa};{g.Data:dd.MM.yyyy};{g.PierwszeWejscie};{g.OstatnieWyjscie};{g.CzasPracy};{g.GodzinyDziesietne:N2};{g.LiczbaWejsc};{g.LiczbaWyjsc};{g.Status}");
                                break;
                            case 2:
                                writer.WriteLine("Pracownik;Grupa;Wejscie;Na terenie;Punkt dostepu");
                                foreach (var o in obecni) writer.WriteLine($"{o.Pracownik};{o.Grupa};{o.Godzina};{o.CzasNaTerenie};{o.PunktDostepu}");
                                break;
                            case 3:
                                writer.WriteLine("Grupa;Pracownicy;Suma godzin;Srednia/os.;Obecni;Braki wyjsc;Przekroczenia");
                                foreach (var p in podsumowania) writer.WriteLine($"{p.Grupa};{p.LiczbaPracownikow};{p.SumaGodzin:N1};{p.SredniaGodzin:N1};{p.Obecni};{p.BrakiWyjsc};{p.Przekroczenia}");
                                break;
                            case 4:
                                writer.WriteLine("Typ;Priorytet;Pracownik;Grupa;Data;Opis");
                                foreach (var a in alerty) writer.WriteLine($"{a.Typ};{a.Priorytet};{a.Pracownik};{a.Grupa};{a.Data:dd.MM.yyyy};{a.Opis}");
                                break;
                        }
                    }
                    MessageBox.Show($"Wyeksportowano do:\n{dialog.FileName}", "Eksport", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Blad eksportu:\n{ex.Message}", "Blad", MessageBoxButton.OK, MessageBoxImage.Error); }
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
