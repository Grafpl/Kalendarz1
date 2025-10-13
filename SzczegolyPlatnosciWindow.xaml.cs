using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows;
using Microsoft.Win32;
using System.Diagnostics;
using Kalendarz1.PrzypomnieniePlatnosci;

namespace Kalendarz1
{
    public partial class SzczegolyPlatnosciWindow : Window
    {
        private string connectionString;
        private string nazwaKontrahenta;
        private List<DokumentPlatnosci> dokumenty;
        private DaneKontrahenta daneKontrahenta;

        public SzczegolyPlatnosciWindow(string connString, string kontrahent)
        {
            InitializeComponent();
            connectionString = connString;
            nazwaKontrahenta = kontrahent;

            txtNazwaKontrahenta.Text = kontrahent;
            txtDataWygenerowania.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            WczytajDaneKontrahenta();
            WczytajDane();
        }

        private void WczytajDaneKontrahenta()
        {
            // Uproszczone zapytanie - tylko podstawowe dane
            string query = @"
    SELECT 
        C.Name AS PelnaNazwa,
        C.Shortcut AS Nazwa,
        ISNULL(C.NIP, '') AS NIP,
        ISNULL(POA.Street, '') AS Ulica,
        ISNULL(POA.PostCode, '') AS KodPocztowy,
        ISNULL(POA.Place, '') AS Miejscowosc
    FROM [HANDEL].[SSCommon].[STContractors] C
    LEFT JOIN [HANDEL].[SSCommon].[STPostOfficeAddresses] POA 
        ON POA.ContactGuid = C.ContactGuid 
        AND POA.AddressName = N'adres domyślny'
    WHERE C.Shortcut = @NazwaKontrahenta";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@NazwaKontrahenta", nazwaKontrahenta);

                    conn.Open();
                    var reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        string ulica = reader["Ulica"]?.ToString()?.Trim() ?? "";
                        string kodPocztowy = reader["KodPocztowy"]?.ToString()?.Trim() ?? "";
                        string miejscowosc = reader["Miejscowosc"]?.ToString()?.Trim() ?? "";

                        daneKontrahenta = new DaneKontrahenta
                        {
                            PelnaNazwa = reader["PelnaNazwa"]?.ToString() ?? "",
                            Nazwa = reader["Nazwa"]?.ToString() ?? "",
                            NIP = reader["NIP"]?.ToString() ?? "",
                            Adres = ulica,
                            KodPocztowy = kodPocztowy,
                            Miejscowosc = miejscowosc
                        };

                        // Aktualizuj interfejs
                        txtPelnaNazwa.Text = daneKontrahenta.PelnaNazwa;

                        // Składanie adresu
                        var adresParts = new List<string>();
                        if (!string.IsNullOrEmpty(ulica)) adresParts.Add(ulica);
                        if (!string.IsNullOrEmpty(kodPocztowy) || !string.IsNullOrEmpty(miejscowosc))
                            adresParts.Add($"{kodPocztowy} {miejscowosc}".Trim());

                        txtAdres.Text = adresParts.Count > 0 ? string.Join("\n", adresParts) : "Brak danych adresowych";
                        txtNIP.Text = !string.IsNullOrEmpty(daneKontrahenta.NIP) ? daneKontrahenta.NIP : "Brak";
                    }
                    else
                    {
                        MessageBox.Show($"Nie znaleziono kontrahenta: {nazwaKontrahenta}", "Uwaga",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd wczytywania danych kontrahenta:\n{ex.Message}",
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void WczytajDane()
        {
            // Stawka odsetek ustawowych (dla 2025 roku - 11,5%)
            decimal stawkaOdsetekRoczna = 11.5m;

            string query = @"
            WITH PNAgg AS (
                SELECT
                    PN.dkid,
                    SUM(ISNULL(PN.kwotarozl, 0)) AS KwotaRozliczona
                FROM [HANDEL].[HM].[PN] AS PN
                GROUP BY PN.dkid
            )
            SELECT
                DK.kod AS NumerDokumentu,
                CONVERT(date, DK.data) AS DataDokumentu,
                CAST(DK.walnetto AS DECIMAL(18, 2)) AS WartoscNetto,
                CAST(DK.walbrutto - DK.walnetto AS DECIMAL(18, 2)) AS WartoscVAT,
                CAST(DK.walbrutto AS DECIMAL(18, 2)) AS WartoscBrutto,
                CAST(ISNULL(PA.KwotaRozliczona, 0) AS DECIMAL(18, 2)) AS Zaplacono,
                CAST((DK.walbrutto - ISNULL(PA.KwotaRozliczona, 0)) AS DECIMAL(18, 2)) AS PozostaloDoZaplaty,
                CONVERT(date, DK.plattermin) AS TerminPlatnosci,
                DATEDIFF(day, CONVERT(date, DK.data), CONVERT(date, DK.plattermin)) AS DniTerminu,
                CASE
                    WHEN GETDATE() > DK.plattermin THEN DATEDIFF(day, DK.plattermin, GETDATE())
                    ELSE 0
                END AS DniPoTerminie
            FROM [HANDEL].[HM].[DK] AS DK
            JOIN [HANDEL].[SSCommon].[STContractors] AS C ON DK.khid = C.id
            LEFT JOIN PNAgg AS PA ON DK.id = PA.dkid
            WHERE
                DK.anulowany = 0
                AND (DK.walbrutto - ISNULL(PA.KwotaRozliczona, 0)) > 0.01
                AND C.Shortcut = @NazwaKontrahenta
            ORDER BY
                TerminPlatnosci ASC;";

            try
            {
                using (var conn = new SqlConnection(connectionString))
                {
                    var cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@NazwaKontrahenta", nazwaKontrahenta);

                    var adapter = new SqlDataAdapter(cmd);
                    var dt = new DataTable();
                    adapter.Fill(dt);

                    dokumenty = new List<DokumentPlatnosci>();

                    decimal sumaWartoscNetto = 0;
                    decimal sumaWartoscVAT = 0;
                    decimal sumaWartoscBrutto = 0;
                    decimal sumaZaplacono = 0;
                    decimal sumaPozostalo = 0;
                    decimal sumaOdsetki = 0;
                    DateTime? najpozniejszyTermin = null;
                    int lp = 1;

                    foreach (DataRow row in dt.Rows)
                    {
                        int dniPoTerminie = Convert.ToInt32(row["DniPoTerminie"]);
                        decimal pozostaloDoZaplaty = Convert.ToDecimal(row["PozostaloDoZaplaty"]);

                        // Oblicz odsetki ustawowe
                        decimal odsetki = 0;
                        if (dniPoTerminie > 0 && pozostaloDoZaplaty > 0)
                        {
                            odsetki = (pozostaloDoZaplaty * stawkaOdsetekRoczna / 100 / 365) * dniPoTerminie;
                        }

                        decimal wartoscBrutto = Convert.ToDecimal(row["WartoscBrutto"]);
                        decimal zaplacono = Convert.ToDecimal(row["Zaplacono"]);
                        decimal procentZaplacone = wartoscBrutto > 0 ? (zaplacono / wartoscBrutto) * 100 : 0;

                        var dok = new DokumentPlatnosci
                        {
                            Lp = lp++,
                            NumerDokumentu = row["NumerDokumentu"].ToString(),
                            DataDokumentu = Convert.ToDateTime(row["DataDokumentu"]),
                            WartoscNetto = Convert.ToDecimal(row["WartoscNetto"]),
                            WartoscVAT = Convert.ToDecimal(row["WartoscVAT"]),
                            WartoscBrutto = wartoscBrutto,
                            Zaplacono = zaplacono,
                            PozostaloDoZaplaty = pozostaloDoZaplaty,
                            TerminPlatnosci = Convert.ToDateTime(row["TerminPlatnosci"]),
                            DniTerminu = Convert.ToInt32(row["DniTerminu"]),
                            DniPoTerminie = dniPoTerminie,
                            ProcentZaplacone = procentZaplacone,
                            Odsetki = odsetki
                        };

                        dokumenty.Add(dok);

                        sumaWartoscNetto += dok.WartoscNetto;
                        sumaWartoscVAT += dok.WartoscVAT;
                        sumaWartoscBrutto += dok.WartoscBrutto;
                        sumaZaplacono += dok.Zaplacono;
                        sumaPozostalo += dok.PozostaloDoZaplaty;
                        sumaOdsetki += dok.Odsetki;

                        if (!najpozniejszyTermin.HasValue || dok.TerminPlatnosci < najpozniejszyTermin.Value)
                        {
                            najpozniejszyTermin = dok.TerminPlatnosci;
                        }
                    }

                    // Dodaj wiersz sumy
                    if (dokumenty.Count > 0)
                    {
                        decimal procentSumaZaplacone = sumaWartoscBrutto > 0 ? (sumaZaplacono / sumaWartoscBrutto) * 100 : 0;

                        dokumenty.Add(new DokumentPlatnosci
                        {
                            NumerDokumentu = "📊 RAZEM:",
                            WartoscNetto = sumaWartoscNetto,
                            WartoscVAT = sumaWartoscVAT,
                            WartoscBrutto = sumaWartoscBrutto,
                            Zaplacono = sumaZaplacono,
                            PozostaloDoZaplaty = sumaPozostalo,
                            ProcentZaplacone = procentSumaZaplacone,
                            Odsetki = sumaOdsetki
                        });
                    }

                    dgDokumenty.ItemsSource = dokumenty;

                    // Aktualizuj podsumowanie
                    txtSumaWartoscNetto.Text = $"{sumaWartoscNetto:N2} zł";
                    txtSumaWartoscBrutto.Text = $"{sumaWartoscBrutto:N2} zł";
                    txtSumaZaplacono.Text = $"{sumaZaplacono:N2} zł";
                    txtSumaPozostalo.Text = $"{sumaPozostalo:N2} zł";
                    txtNajpozniejszyTermin.Text = najpozniejszyTermin?.ToString("dd.MM.yyyy") ?? "—";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas wczytywania danych: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnGenerujPDF_Click(object sender, RoutedEventArgs e)
        {
            // Wybór wersji
            var wyborWersjiWindow = new WyborWersjiPrzypomnienieWindow();
            if (wyborWersjiWindow.ShowDialog() != true)
                return;

            var wersja = wyborWersjiWindow.WybranaWersja;

            // Pytanie o odsetki
            var result = MessageBox.Show(
                "Czy dołączyć informacje o odsetkach ustawowych do przypomnienia?",
                "Odsetki w dokumencie",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            bool czyDodacOdsetki = result == MessageBoxResult.Yes;

            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"Przypomnienie_Platnosci_{nazwaKontrahenta}_{DateTime.Now:yyyyMMdd}.pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var generator = new PrzypomnieniePlatnosciPDFGenerator();
                    generator.GenerujPDF(
                        saveDialog.FileName,
                        daneKontrahenta,
                        dokumenty.Where(d => d.NumerDokumentu != "📊 RAZEM:").ToList(),
                        czyDodacOdsetki,
                        wersja);

                    MessageBox.Show("✓ PDF został pomyślnie wygenerowany!", "Sukces",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    Process.Start(new ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd generowania PDF: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnWyslijEmail_Click(object sender, RoutedEventArgs e)
        {
            var przeterminowane = dokumenty.Where(d => d.NumerDokumentu != "📊 RAZEM:" && d.DniPoTerminie > 0).ToList();

            if (!przeterminowane.Any())
            {
                MessageBox.Show("Brak przeterminowanych faktur dla tego kontrahenta.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Wybór wersji
            var wyborWersjiWindow = new WyborWersjiPrzypomnienieWindow();
            if (wyborWersjiWindow.ShowDialog() != true)
                return;

            var wersja = wyborWersjiWindow.WybranaWersja;

            decimal kwotaPrzeterminowana = przeterminowane.Sum(d => d.PozostaloDoZaplaty);
            int liczbaDokumentow = przeterminowane.Count;
            DateTime najpozniejszyTermin = przeterminowane.Min(d => d.TerminPlatnosci);

            // Generuj PDF
            var saveDialog = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"Przypomnienie_Platnosci_{nazwaKontrahenta}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            string sciezkaPDF = "";
            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var resultOdsetki = MessageBox.Show(
                        "Czy dołączyć informacje o odsetkach?",
                        "Odsetki",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    bool czyDodacOdsetki = resultOdsetki == MessageBoxResult.Yes;

                    var generator = new PrzypomnieniePlatnosciPDFGenerator();
                    generator.GenerujPDF(
                        saveDialog.FileName,
                        daneKontrahenta,
                        dokumenty.Where(d => d.NumerDokumentu != "📊 RAZEM:").ToList(),
                        czyDodacOdsetki,
                        wersja);

                    sciezkaPDF = saveDialog.FileName;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Błąd generowania PDF: {ex.Message}", "Błąd",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                return;
            }

            // Otwórz okno emaila
            string emailOdbiorcy = "";
            var emailWindow = new EmailPrzypomnienieWindow(
                nazwaKontrahenta,
                emailOdbiorcy,
                kwotaPrzeterminowana,
                liczbaDokumentow,
                najpozniejszyTermin,
                sciezkaPDF,
                wersja);

            emailWindow.ShowDialog();
        }

        private void BtnDrukuj_Click(object sender, RoutedEventArgs e)
        {
            // Wybór wersji
            var wyborWersjiWindow = new WyborWersjiPrzypomnienieWindow();
            if (wyborWersjiWindow.ShowDialog() != true)
                return;

            var wersja = wyborWersjiWindow.WybranaWersja;

            try
            {
                var result = MessageBox.Show(
                    "Czy dołączyć informacje o odsetkach?",
                    "Odsetki",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                bool czyDodacOdsetki = result == MessageBoxResult.Yes;

                string tempPDF = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    $"Przypomnienie_Druk_{DateTime.Now:yyyyMMddHHmmss}.pdf");

                var generator = new PrzypomnieniePlatnosciPDFGenerator();
                generator.GenerujPDF(
                    tempPDF,
                    daneKontrahenta,
                    dokumenty.Where(d => d.NumerDokumentu != "📊 RAZEM:").ToList(),
                    czyDodacOdsetki,
                    wersja);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = tempPDF,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);

                MessageBox.Show("✓ Dokument został otwarty. Użyj opcji drukowania w przeglądarce PDF.", "Informacja",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Błąd: {ex.Message}", "Błąd",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BtnZamknij_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    public class DokumentPlatnosci
    {
        public int Lp { get; set; }
        public string NumerDokumentu { get; set; }
        public DateTime DataDokumentu { get; set; }
        public decimal WartoscNetto { get; set; }
        public decimal WartoscVAT { get; set; }
        public decimal WartoscBrutto { get; set; }
        public decimal Zaplacono { get; set; }
        public decimal PozostaloDoZaplaty { get; set; }
        public DateTime TerminPlatnosci { get; set; }
        public int DniTerminu { get; set; }
        public int DniPoTerminie { get; set; }
        public decimal ProcentZaplacone { get; set; }
        public decimal Odsetki { get; set; }
        public bool JestPoTerminie => DniPoTerminie > 0;
    }

    public class DaneKontrahenta
    {
        public string PelnaNazwa { get; set; }
        public string Nazwa { get; set; }
        public string NIP { get; set; }
        public string Adres { get; set; }
        public string KodPocztowy { get; set; }
        public string Miejscowosc { get; set; }
    }
}