using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace Kalendarz1
{
    public partial class SzczegolyDokumentuWindow : Window
    {
        private string connectionString;
        private int idDokumentu;
        private string numerDokumentu;

        public SzczegolyDokumentuWindow(string connString, int dokId, string numerDok)
        {
            InitializeComponent();
            connectionString = connString;
            idDokumentu = dokId;
            numerDokumentu = numerDok;

            txtNumerDokumentu.Text = $"📄 Faktura: {numerDokumentu}";
            this.Title = $"Szczegóły faktury: {numerDokumentu}";

            this.Loaded += SzczegolyDokumentuWindow_Loaded;
        }

        private void SzczegolyDokumentuWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                WczytajInformacjeODokumencie();
                WczytajPozycjeDokumentu();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas ładowania szczegółów dokumentu:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void WczytajInformacjeODokumencie()
        {
            string query = @"
WITH PNAgg AS (
    SELECT 
        PN.dkid,
        SUM(ISNULL(PN.kwotarozl, 0)) AS KwotaRozliczona
    FROM [HANDEL].[HM].[PN] PN
    GROUP BY PN.dkid
)
SELECT 
    DK.kod AS NumerDokumentu,
    CONVERT(date, DK.data) AS DataWystawienia,
    CONVERT(date, DK.plattermin) AS TerminPlatnosci,
    DATEDIFF(day, DK.data, DK.plattermin) AS DniTerminu,
    C.shortcut AS NazwaKontrahenta,
    ISNULL(WYM.CDim_Handlowiec_Val, 'Nieprzypisany') AS Handlowiec,
    CAST(DK.walnetto AS DECIMAL(18,2)) AS WartoscNetto,
    CAST(DK.walbrutto AS DECIMAL(18,2)) AS WartoscBrutto,
    CAST(ISNULL(PA.KwotaRozliczona, 0) AS DECIMAL(18,2)) AS Zaplacono,
    CAST((DK.walbrutto - ISNULL(PA.KwotaRozliczona, 0)) AS DECIMAL(18,2)) AS PozostaloDoZaplaty,
    CASE 
        WHEN (DK.walbrutto - ISNULL(PA.KwotaRozliczona, 0)) <= 0.01 THEN NULL
        WHEN DK.plattermin > GETDATE() THEN DATEDIFF(day, GETDATE(), DK.plattermin)
        WHEN DK.plattermin <= GETDATE() THEN -DATEDIFF(day, DK.plattermin, GETDATE())
        ELSE NULL
    END AS DniDoTerminu
FROM [HANDEL].[HM].[DK] DK
INNER JOIN [HANDEL].[SSCommon].[STContractors] C ON DK.khid = C.id
LEFT JOIN [HANDEL].[SSCommon].[ContractorClassification] WYM ON DK.khid = WYM.ElementId
LEFT JOIN PNAgg PA ON DK.id = PA.dkid
WHERE DK.id = @idDokumentu;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@idDokumentu", idDokumentu);

                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            txtDataWystawienia.Text = reader["DataWystawienia"] != DBNull.Value
                                ? Convert.ToDateTime(reader["DataWystawienia"]).ToString("dd.MM.yyyy")
                                : "---";

                            txtTerminPlatnosci.Text = reader["TerminPlatnosci"] != DBNull.Value
                                ? Convert.ToDateTime(reader["TerminPlatnosci"]).ToString("dd.MM.yyyy")
                                : "---";

                            txtDniTerminu.Text = reader["DniTerminu"] != DBNull.Value
                                ? $"{reader["DniTerminu"]} dni"
                                : "---";

                            txtKontrahent.Text = reader["NazwaKontrahenta"]?.ToString() ?? "---";
                            txtHandlowiec.Text = reader["Handlowiec"]?.ToString() ?? "---";

                            txtWartoscNetto.Text = reader["WartoscNetto"] != DBNull.Value
                                ? $"{Convert.ToDecimal(reader["WartoscNetto"]):N2} zł"
                                : "---";

                            txtWartoscBrutto.Text = reader["WartoscBrutto"] != DBNull.Value
                                ? $"{Convert.ToDecimal(reader["WartoscBrutto"]):N2} zł"
                                : "---";

                            if (reader["DniDoTerminu"] == DBNull.Value)
                            {
                                txtStatusPlatnosci.Text = "✓ Zapłacone";
                                txtStatusPlatnosci.Foreground = new SolidColorBrush(Color.FromRgb(92, 138, 58)); // #5C8A3A
                                txtDniDoTerminu.Text = "✓ Zapłacone";
                                txtDniDoTerminu.Foreground = new SolidColorBrush(Color.FromRgb(92, 138, 58));
                            }
                            else
                            {
                                int dni = Convert.ToInt32(reader["DniDoTerminu"]);
                                decimal pozostalo = Convert.ToDecimal(reader["PozostaloDoZaplaty"]);

                                if (dni > 0)
                                {
                                    txtStatusPlatnosci.Text = $"Do zapłaty: {pozostalo:N2} zł";
                                    txtStatusPlatnosci.Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                                    txtDniDoTerminu.Text = $"{dni} dni";
                                    txtDniDoTerminu.Foreground = new SolidColorBrush(Color.FromRgb(52, 152, 219));
                                }
                                else if (dni == 0)
                                {
                                    txtStatusPlatnosci.Text = $"⚠ Termin dziś: {pozostalo:N2} zł";
                                    txtStatusPlatnosci.Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18));
                                    txtDniDoTerminu.Text = "⚠ Dziś";
                                    txtDniDoTerminu.Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18));
                                }
                                else
                                {
                                    txtStatusPlatnosci.Text = $"⚠ Po terminie: {pozostalo:N2} zł";
                                    txtStatusPlatnosci.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                                    txtDniDoTerminu.Text = $"⚠ Po terminie ({Math.Abs(dni)} dni)";
                                    txtDniDoTerminu.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania informacji o dokumencie: {ex.Message}", ex);
            }
        }

        private void WczytajPozycjeDokumentu()
        {
            string query = @"
                SELECT 
                    DP.lp AS Lp,
                    DP.kod AS KodTowaru, 
                    DP.ilosc AS Ilosc, 
                    DP.cena AS Cena, 
                    DP.wartNetto AS Wartosc 
                FROM [HANDEL].[HM].[DP] DP 
                WHERE DP.super = @idDokumentu 
                ORDER BY DP.lp;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@idDokumentu", idDokumentu);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    var pozycje = new List<PozycjaFaktury>();
                    decimal sumaIlosc = 0;
                    decimal sumaWartosc = 0;
                    decimal maxCena = 0;
                    decimal minCena = decimal.MaxValue;
                    string najdrozszaPozycja = "---";
                    string najtanszaPozycja = "---";

                    foreach (DataRow row in dt.Rows)
                    {
                        decimal ilosc = row["Ilosc"] != DBNull.Value ? Convert.ToDecimal(row["Ilosc"]) : 0;
                        decimal cena = row["Cena"] != DBNull.Value ? Convert.ToDecimal(row["Cena"]) : 0;
                        decimal wartosc = row["Wartosc"] != DBNull.Value ? Convert.ToDecimal(row["Wartosc"]) : 0;

                        pozycje.Add(new PozycjaFaktury
                        {
                            Lp = row["Lp"] != DBNull.Value ? Convert.ToInt32(row["Lp"]) : 0,
                            KodTowaru = row["KodTowaru"]?.ToString() ?? "",
                            Ilosc = ilosc,
                            IloscFormatted = $"{ilosc:N2} kg",
                            Cena = cena,
                            CenaFormatted = $"{cena:N2} zł/kg",
                            Wartosc = wartosc,
                            WartoscFormatted = $"{wartosc:N2} zł"
                        });

                        sumaIlosc += ilosc;
                        sumaWartosc += wartosc;

                        if (cena > maxCena)
                        {
                            maxCena = cena;
                            najdrozszaPozycja = row["KodTowaru"]?.ToString() ?? "---";
                        }
                        if (cena < minCena && cena > 0)
                        {
                            minCena = cena;
                            najtanszaPozycja = row["KodTowaru"]?.ToString() ?? "---";
                        }
                    }

                    // Oblicz udziały procentowe
                    foreach (var pozycja in pozycje)
                    {
                        pozycja.UdzialProcentowy = sumaWartosc > 0
                            ? $"{(pozycja.Wartosc / sumaWartosc * 100):N1}%"
                            : "0%";
                    }

                    dgPozycje.ItemsSource = pozycje;

                    // Podstawowe statystyki
                    decimal sredniaCena = sumaIlosc > 0 ? sumaWartosc / sumaIlosc : 0;
                    decimal sredniaWartoscPozycji = pozycje.Count > 0 ? sumaWartosc / pozycje.Count : 0;

                    txtStatWartoscCalk.Text = $"{sumaWartosc:N2} zł";
                    txtStatLiczbaPozycji.Text = pozycje.Count.ToString();
                    txtStatSumaIlosci.Text = $"{sumaIlosc:N2} kg";
                    txtStatSredniaCena.Text = $"{sredniaCena:N2} zł/kg";
                    txtStatSredniaWartoscPozycji.Text = $"{sredniaWartoscPozycji:N2} zł";

                    // Top 3 pozycje
                    var top3 = pozycje.OrderByDescending(p => p.Wartosc).Take(3).ToList();
                    var top3Display = new List<Top3Pozycja>();

                    for (int i = 0; i < top3.Count; i++)
                    {
                        var p = top3[i];
                        string ranking = i == 0 ? "🥇" : i == 1 ? "🥈" : "🥉";
                        top3Display.Add(new Top3Pozycja
                        {
                            Ranking = ranking,
                            Nazwa = p.KodTowaru.Length > 30 ? p.KodTowaru.Substring(0, 30) + "..." : p.KodTowaru,
                            Szczegoly = $"{p.IloscFormatted} × {p.CenaFormatted}",
                            Udzial = p.UdzialProcentowy
                        });
                    }
                    icTop3Pozycje.ItemsSource = top3Display;

                    // Indeks koncentracji (HHI - Herfindahl-Hirschman Index)
                    decimal hhi = 0;
                    foreach (var p in pozycje)
                    {
                        decimal udzial = sumaWartosc > 0 ? p.Wartosc / sumaWartosc : 0;
                        hhi += udzial * udzial;
                    }
                    hhi *= 100; // Skalowanie 0-100

                    pbKoncentracja.Value = (double)hhi;
                    txtKoncentracja.Text = $"{hhi:N1}%";

                    if (hhi > 50)
                    {
                        txtKoncentracjaOpis.Text = "⚠ Wysoką koncentrację - dominuje kilka pozycji";
                        txtKoncentracjaOpis.Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60));
                    }
                    else if (hhi > 25)
                    {
                        txtKoncentracjaOpis.Text = "✓ Umiarkowana koncentracja wartości";
                        txtKoncentracjaOpis.Foreground = new SolidColorBrush(Color.FromRgb(243, 156, 18));
                    }
                    else
                    {
                        txtKoncentracjaOpis.Text = "✓ Równomierny rozkład wartości";
                        txtKoncentracjaOpis.Foreground = new SolidColorBrush(Color.FromRgb(92, 138, 58));
                    }

                    // Rozpiętość cen
                    txtMinCena.Text = minCena != decimal.MaxValue ? $"{minCena:N2} zł/kg" : "---";
                    txtMaxCena.Text = maxCena > 0 ? $"{maxCena:N2} zł/kg" : "---";

                    // Najdroższe/najtańsze
                    txtStatNajdrozsza.Text = najdrozszaPozycja;
                    txtStatNajtansza.Text = najtanszaPozycja;

                    // Diagnoza
                    string diagnoza = GenerujDiagnoze(pozycje.Count, hhi, sumaWartosc, sredniaWartoscPozycji);
                    txtDiagnoza.Text = diagnoza;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Błąd podczas pobierania pozycji: {ex.Message}", ex);
            }
        }

        private string GenerujDiagnoze(int liczbaPozycji, decimal hhi, decimal sumaWartosc, decimal sredniaWartosc)
        {
            if (liczbaPozycji == 1)
                return "💡 Faktura zawiera tylko jedną pozycję.";

            if (hhi > 50)
                return $"💡 Faktura zdominowana przez {(liczbaPozycji > 3 ? "kilka" : "jedną")} główną pozycję o wysokiej wartości.";

            if (liczbaPozycji > 10)
                return "💡 Faktura zawiera bogaty asortyment produktów o zróżnicowanej wartości.";

            if (sredniaWartosc > 500)
                return "💡 Faktura zawiera pozycje o wysokiej jednostkowej wartości.";

            return "💡 Faktura zawiera standardowy zestaw pozycji o zrównoważonej wartości.";
        }

        private void BtnEksportuj_Click(object sender, RoutedEventArgs e)
        {
            if (dgPozycje.Items.Count == 0)
            {
                MessageBox.Show("ℹ Brak danych do eksportu", "Informacja",
                              MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"Faktura_{numerDokumentu.Replace("/", "-")}_{DateTime.Now:yyyyMMdd}.csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var csv = new StringBuilder();
                    csv.AppendLine("Lp;Kod Towaru;Ilość;Cena Netto;Wartość Netto;Udział %");

                    foreach (PozycjaFaktury pozycja in dgPozycje.Items)
                    {
                        csv.AppendLine($"{pozycja.Lp};{pozycja.KodTowaru};{pozycja.Ilosc:N2};{pozycja.Cena:N2};{pozycja.Wartosc:N2};{pozycja.UdzialProcentowy}");
                    }

                    File.WriteAllText(saveDialog.FileName, csv.ToString(), Encoding.UTF8);
                    MessageBox.Show("✓ Eksport zakończony pomyślnie!", "Sukces",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Błąd eksportu: {ex.Message}", "Błąd",
                                  MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnDrukuj_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funkcja drukowania będzie dostępna wkrótce.",
                "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class PozycjaFaktury
    {
        public int Lp { get; set; }
        public string KodTowaru { get; set; }
        public decimal Ilosc { get; set; }
        public string IloscFormatted { get; set; }
        public decimal Cena { get; set; }
        public string CenaFormatted { get; set; }
        public decimal Wartosc { get; set; }
        public string WartoscFormatted { get; set; }
        public string UdzialProcentowy { get; set; }
    }

    public class Top3Pozycja
    {
        public string Ranking { get; set; }
        public string Nazwa { get; set; }
        public string Szczegoly { get; set; }
        public string Udzial { get; set; }
    }
}